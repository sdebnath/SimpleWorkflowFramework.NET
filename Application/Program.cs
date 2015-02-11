//----------------------------------------------------------------------------------------------------------------------
//  Copyright (c) 2013, Shawn Debnath. All rights reserved.
//    
//  Redistribution and use in source and binary forms, with or without
//  modification, are permitted provided that the following conditions are met:
//
//      * Redistributions of source code must retain the above copyright
//        notice, this list of conditions and the following disclaimer.
//      * Redistributions in binary form must reproduce the above copyright
//        notice, this list of conditions and the following disclaimer in the
//        documentation and/or other materials provided with the distribution.
//    
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
//  ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
//  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//  DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
//  DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
//  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
//  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
//  ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
//  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Amazon;
using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;
using Application.Workflows;
using Newtonsoft.Json;
using SimpleWorkflowFramework.NET;

namespace Application
{
    /// <summary>
    /// A simple test program that allows me to debug the SimpleWorkflowFramework.NET by invoking workflows 
    /// and simulating activities. This doesn't follow any good practices and wasn't intended for use in critical 
    /// software. 
    /// </summary>
    internal class Program
    {
        public static void Main(string[] args)
        {
            // Define the workflows that we know of that event processor will be handling
            var workflows = new Dictionary<string, Type>
                {
                    {"CustomerOrderWorkflow", typeof (CustomerOrderWorkflow)},
                    {"VerifyCustomerWorkflow", typeof (VerifyCustomerWorkflow)}
                };


            // Stopwatch to see how well we are performing
            var stopwatch = new Stopwatch();

            // We will use this ID as our decision task ID and activity task ID to identify ourselves when polling for
            // decision and activity tasks.
            var workflowWorkerIdentity = Guid.NewGuid();
            
            // Print out our AWS SWF domains, workflows and activities
            Console.Write(GetServiceOutput());

            var loop = true;
            do
            {
                // Our super simple application menu
                Console.WriteLine("");
                Console.WriteLine("=============");
                Console.WriteLine("| Main Menu |");
                Console.WriteLine("=============");
                Console.WriteLine("[1] Submit a new workflow");
                Console.WriteLine("[2] Wait for decide using a decision task");
                Console.WriteLine("[3] Wait for and do some work for an activity task");
                Console.WriteLine("[4] Quit");

                Console.Write("\nChoice: ");
                var key = Console.ReadLine();

                if (String.IsNullOrEmpty(key))
                {
                    continue;
                }

                switch (key)
                {
                    // Initiate a workflow execution
                    case "1":
                        {
                            Console.WriteLine("Option [1] selected - Submit a new workflow");

                            // SWF client is disposable, so dispose it
                            using (var swfClient = new AmazonSimpleWorkflowClient(RegionEndpoint.USWest2))
                            {
                                // Our simple property bag: we just need to the email for the account
                                var propertyBag = new Dictionary<string, object> { { "SampleOrderNumber", "12345" } };

                                // Setup the workflow request
                                var workflowRequest = new StartWorkflowExecutionRequest
                                {
                                    Domain = "demo-domain",
                                    WorkflowId = Guid.NewGuid().ToString(),
                                    WorkflowType = new WorkflowType
                                    {
                                        Name = "CustomerOrderWorkflow",
                                        Version = "1.0"
                                    },
                                    Input = JsonConvert.SerializeObject(propertyBag)
                                };

                                try
                                {
                                    // Call AWS SWF and submit the workflow request
                                    swfClient.StartWorkflowExecution(workflowRequest);
                                }
                                catch (AmazonSimpleWorkflowException ex)
                                {
                                    Console.WriteLine("Caught Exception: " + ex.Message);
                                    Console.WriteLine("Response Status Code: " + ex.StatusCode);
                                    Console.WriteLine("Error Code: " + ex.ErrorCode);
                                    Console.WriteLine("Error Type: " + ex.ErrorType);
                                    Console.WriteLine("Request ID: " + ex.RequestId);
                                    Console.WriteLine("Data: " + ex.Data);
                                    Console.WriteLine("Stacktrace: " + ex.StackTrace);
                                }
                            }
                        }
                        break;

                    // Poll for decision task
                    case "2":
                        {
                            Console.WriteLine("Option [2] selected - Wait for decide using a decision task");
                            Console.WriteLine("Waiting...");

                            // SWF client is disposable, so dispose it
                            using (var swfClient = new AmazonSimpleWorkflowClient(RegionEndpoint.USWest2))
                            {
                                try
                                {
                                    // Setup the decision request
                                    var decisionTaskRequest = new PollForDecisionTaskRequest
                                    {
                                        Domain = "demo-domain",
                                        Identity = workflowWorkerIdentity.ToString(),
                                        TaskList = new TaskList { Name = "DeciderTaskList-Default" }
                                    };

                                    // Call AWS SWF and wait for (default timeout: 60 secs) a decision task
                                    var decisionTaskResponse = swfClient.PollForDecisionTask(decisionTaskRequest);

                                    // Task token being an empty string means there are no tasks available and 
                                    // we are past the 60 seconds that AWS holds a connection in case a task
                                    // becomes available. If this is the case, we simply retry.
                                    var taskToken =
                                        decisionTaskResponse.DecisionTask.TaskToken;
                                    if (!String.IsNullOrEmpty(taskToken))
                                    {
                                        // We have a valid task, do something...
                                        var decisionTask =
                                            decisionTaskResponse.DecisionTask;

                                        switch (decisionTask.WorkflowType.Name)
                                        {
                                            case "CustomerOrderWorkflow":
                                            case "VerifyCustomerWorkflow":
                                                {
                                                    Debug.Assert(decisionTask.WorkflowType.Version == "1.0");
                                                }
                                                break;

                                            default:
                                                Console.WriteLine("ERROR: Unknown workflow.");
                                                break;
                                        }

                                        // Define a new WorkflowEventsProcessor object and let it make the decision!
                                        stopwatch.Start();
                                        var workflowProcessor = new WorkflowEventsProcessor(decisionTask, workflows);
                                        var decisionRequest = workflowProcessor.Decide();
                                        stopwatch.Stop();

                                        Console.WriteLine(">>> Decision(s) made in " + stopwatch.ElapsedMilliseconds + "ms");

                                        // We have our decision, send it away and do something 
                                        // more productive with the response
                                        swfClient.RespondDecisionTaskCompleted(decisionRequest);
                                    }
                                }
                                catch (AmazonSimpleWorkflowException ex)
                                {
                                    Console.WriteLine("Caught Exception: " + ex.Message);
                                    Console.WriteLine("Response Status Code: " + ex.StatusCode);
                                    Console.WriteLine("Error Code: " + ex.ErrorCode);
                                    Console.WriteLine("Error Type: " + ex.ErrorType);
                                    Console.WriteLine("Request ID: " + ex.RequestId);
                                    Console.WriteLine("Data: " + ex.Data);
                                    Console.WriteLine("Stacktrace: " + ex.StackTrace);
                                }
                            }
                        }
                        break;

                    // Poll for activity task
                    case "3":
                        {
                            Console.WriteLine("Option [3] selected - Wait for decide using a activity task");
                            Console.WriteLine("Waiting...");

                            // SWF client is disposable, so dispose it
                            using (var swfClient = new AmazonSimpleWorkflowClient(RegionEndpoint.USWest2))
                            {
                                try
                                {
                                    // Setup the activity request
                                    var activityTaskRequest = new PollForActivityTaskRequest
                                    {
                                        Domain = "demo-domain",
                                        Identity = workflowWorkerIdentity.ToString(),
                                        TaskList = new TaskList { Name = "ActivityTaskList-Default" }
                                    };

                                    // Call AWS SWF and wait for (default timeout: 60 secs) a activity task
                                    var activityTaskResponse = swfClient.PollForActivityTask(activityTaskRequest);

                                    // Task token being an empty string means there are no tasks available and 
                                    // we are past the 60 seconds that AWS holds a connection in case a task
                                    // becomes available. If this is the case, we simply retry.
                                    var taskToken =
                                        activityTaskResponse.ActivityTask.TaskToken;
                                    if (!String.IsNullOrEmpty(taskToken))
                                    {
                                        // We have a valid task, do something...
                                        var activityTask =
                                            activityTaskResponse.ActivityTask;

                                        Console.WriteLine("\n");
                                        Console.WriteLine(">>> Activity: " + activityTask.ActivityType.Name);

                                        // In the real world we would define the activity code in a separate object
                                        // and fire off a thread to actually work on it but in this case we are just
                                        // testing the workflow so this suffices
                                        switch (activityTask.ActivityType.Name)
                                        {
                                            // CustomerOrderWorkflow activities
                                            case "VerifyOrder":
                                            case "ShipOrder":
                                                {
                                                    Debug.Assert(activityTask.ActivityType.Version == "1.0");
                                                }
                                                break;

                                            // VerifyCustomerWorkflow activities
                                            case "VerifyCustomerAddress":
                                            case "CheckFraudDB":
                                            case "ChargeCreditCard":
                                                {
                                                    Debug.Assert(activityTask.ActivityType.Version == "1.0");
                                                }
                                                break;

                                            default:
                                                Console.WriteLine("ERROR: Unknown activity.");
                                                break;
                                        }

                                        var activityCompletedRequest = new RespondActivityTaskCompletedRequest
                                        {
                                            TaskToken = activityTask.TaskToken,
                                            Result = activityTask.Input
                                        };

                                        // Completion request setup complete, send it away. NOTE: Do something more
                                        // productive with the response
                                        swfClient.RespondActivityTaskCompleted(activityCompletedRequest);

                                        //var activityFailedRequest = new RespondActivityTaskFailedRequest
                                        //    {
                                        //        TaskToken = activityTask.TaskToken,
                                        //        Details = "Test failure."
                                        //    };
                                        //// Completion request setup complete, send it away. NOTE: Do something more
                                        //// productive with the response
                                        //swfClient.RespondActivityTaskFailed(activityFailedRequest);
                                    }
                                }
                                catch (AmazonSimpleWorkflowException ex)
                                {
                                    Console.WriteLine("Caught Exception: " + ex.Message);
                                    Console.WriteLine("Response Status Code: " + ex.StatusCode);
                                    Console.WriteLine("Error Code: " + ex.ErrorCode);
                                    Console.WriteLine("Error Type: " + ex.ErrorType);
                                    Console.WriteLine("Request ID: " + ex.RequestId);
                                    Console.WriteLine("Data: " + ex.Data);
                                    Console.WriteLine("Stacktrace: " + ex.StackTrace);
                                }
                            }
                        }
                        break;

                    case "4":
                        // Quit
                        loop = false;
                        break;

                    default:
                        Console.WriteLine("ERROR: Unknown command.");
                        break;
                }
            } while (loop);
        }

        public static string GetServiceOutput()
        {
            var sb = new StringBuilder(1024);
            using (var sr = new StringWriter(sb))
            {
                sr.WriteLine("===============================");
                sr.WriteLine("| AWS Simple Workflow Service |");
                sr.WriteLine("===============================");

                try
                {
                    // Print the available domains, workflows and activities. Region endpoint depends on where
                    // you chose to set up your workflow domain
                    var swfClient = new AmazonSimpleWorkflowClient(RegionEndpoint.USWest2);

                    sr.WriteLine();
                    var listDomainRequest = new ListDomainsRequest
                    {
                        RegistrationStatus = "REGISTERED"
                    };

                    var listDomainResponse = swfClient.ListDomains(listDomainRequest);
                    foreach (var domain in listDomainResponse.DomainInfos.Infos)
                    {
                        sr.WriteLine("[" + domain.Name + "]");
                        sr.WriteLine("status: " + domain.Status);
                        sr.WriteLine("description: " + domain.Description);

                        sr.WriteLine("\n  WORKFLOWS");
                        var listWorkflowRequest = new ListWorkflowTypesRequest
                        {
                            Domain = domain.Name,
                            RegistrationStatus = "REGISTERED"
                        };
                        var listWorkflowTypesResponse = swfClient.ListWorkflowTypes(listWorkflowRequest);
                        foreach (
                            var workflow in
                                listWorkflowTypesResponse.WorkflowTypeInfos.TypeInfos)
                        {
                            sr.WriteLine("  [" + workflow.WorkflowType.Name + "] (" + workflow.WorkflowType.Version + ") " + workflow.Status);
                            sr.WriteLine("  creation: " + workflow.CreationDate);
                            sr.WriteLine("  deprecation: " + workflow.DeprecationDate);
                            sr.WriteLine("  description:" + TrimStringToLength(workflow.Description, 60, "", "              "));
                            sr.WriteLine();
                        }

                        sr.WriteLine("\n  ACTIVITIES");
                        var listActivityRequest = new ListActivityTypesRequest
                        {
                            Domain = domain.Name,
                            RegistrationStatus = "REGISTERED"
                        };
                        var listActivityResponse = swfClient.ListActivityTypes(listActivityRequest);
                        foreach (
                            var activity in listActivityResponse.ActivityTypeInfos.TypeInfos)
                        {
                            sr.WriteLine("  [" + activity.ActivityType.Name + "] (" + activity.ActivityType.Version + ") " + activity.Status);
                            sr.WriteLine("  creation: " + activity.CreationDate);
                            sr.WriteLine("  deprecation: " + activity.DeprecationDate);
                            sr.WriteLine("  description:" + TrimStringToLength(activity.Description, 60, "", "              "));
                            sr.WriteLine();
                        }
                    }
                }
                catch (AmazonSimpleWorkflowException ex)
                {
                    if (ex.ErrorCode != null && ex.ErrorCode.Equals("AuthFailure"))
                    {
                        sr.WriteLine("The account you are using is not signed up for Amazon SWF.");
                        sr.WriteLine("You can sign up for Amazon SWF at http://aws.amazon.com/swf");
                    }
                    else
                    {
                        sr.WriteLine("Caught Exception: " + ex.Message);
                        sr.WriteLine("Response Status Code: " + ex.StatusCode);
                        sr.WriteLine("Error Code: " + ex.ErrorCode);
                        sr.WriteLine("Error Type: " + ex.ErrorType);
                        sr.WriteLine("Request ID: " + ex.RequestId);
                        sr.WriteLine("Data: " + ex.Data);
                        sr.WriteLine("Stacktrace: " + ex.StackTrace);
                    }
                }
                sr.WriteLine();
            }
            return sb.ToString();
        }

        public static string TrimStringToLength(string s, int len, string firstPrefix, string prefix)
        {
            var parts = s.Split(' ');
            var sb = new StringBuilder();

            var lineLength = 0;
            sb.Append(firstPrefix);
            foreach (string part in parts)
            {
                if (lineLength + part.Length > len)
                {
                    sb.Append("\n" + prefix);
                    lineLength = 0;
                }

                sb.Append(' ');
                sb.Append(part);
                lineLength += part.Length;
            }

            return sb.ToString();
        }
    }
}