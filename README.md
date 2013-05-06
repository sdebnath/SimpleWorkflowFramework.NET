SimpleWorkflowFramework.NET
===========================

SimpleWorkflowFramework.NET is a helper library to simplify writing code for Amazon Web Services (AWS) Simple Workflow
Framework (SWF) in C#. Currently, AWS hasn't released a Flow framework for the C# language, so developers have to 
write their own event handling logic if they wish to use SWF from C#. 

As part of trying to understand the semantics of SWF, I ended up writing a library that generalizes the common code
and greatly simplifies writing simple workflows. SimpleWorkflowFramework.NET is the result.

As of today, the events we callback on are:

*   WorkflowExecutionStarted
*   WorkflowExecutionContinuedAsNew
*   ActivityTaskCompleted
*   ActivityTaskFailed
*   ActivityTaskTimedOut
*   ScheduleActivityTaskFailed
*   ChildWorkflowExecutionStarted
*   ChildWorkflowExecutionCompleted
*   ChildWorkflowExecutionFailed
*   ChildWorkflowExecutionTerminated
*   ChildWorkflowExecutionTimedOut
*   StartChildWorkflowExecutionFailed

Features not _yet_ implemented:

*   Timers
*   Cancellations
*   Signals

To implement a workflow to receive event callbacks, you will need to implement the IWorkflowDecisionMaker interface
or inherit WorkflowBase which contains a default implementation for handling the common case workflows.  

Below I outline a simple use case based on the canonical Amazon workflow example:


    [Customer Order Workflow]                                                                                     
                   +--------------+                                                 +------------+          		 
    (start)   =>   | Verify Order |   =>   < Verify Customer Child Workflow>   =>   | Ship Order |   => (end)	 
                   +--------------+                                                 +------------+				 
    			                                         ^															 
    												   /   \														 
          .-------------------------------------------       -----------------------------------------------.	 
         /                                                                                                   \	 
                   +-------------------------+        +----------------+        +--------------------+          	 
    (start)   =>   | Verify Customer Address |   =>   | Check Fraud DB |   =>   | Charge Credit Card |   => (end) 
                   +-------------------------+        +----------------+        +--------------------+			 
    [Verify Customer Workflow]                                                                                    



In order to run the code you will need an AWS account and the following workflows and activities need to be registered:

#### Workflows
*   CustomerOrderWorkflow
*   VerifyCustomerWorkflow

##### Common properties for the workflows
*   Version: 1.0
*   TaskList: DeciderTaskList-Default
*   Default Timeouts: 60 seconds
*   Child Policy: Terminate

#### Activities
*   ChargeCreditCard
*   CheckFraudDB
*   ShipOrder
*   VerifyCustomerAddress
*   VerifyOrder

##### Common properties for the workflows
*   Version: 1.0
*   TaskList: ActivityTaskList-Default
*   Default Timeouts: 60 seconds

Once the activities and workflows are registered, we need to define a dictionary in our code to let event processor
know which workflows we want it handle and call back on:

    // Define the workflows that we know of that event processor will be handling
    var workflows = new Dictionary<string, Type>
        {
            {"CustomerOrderWorkflow", typeof (CustomerOrderWorkflow)},
            {"VerifyCustomerWorkflow", typeof (VerifyCustomerWorkflow)}
        };

Then wait for a decision and call the events processor to handle the event:

    var workflowProcessor = new WorkflowEventsProcessor(decisionTask, workflows);
    var decisionRequest = workflowProcessor.Decide();
	
And here's the definition for a workflow:

    public class CustomerOrderWorkflow : WorkflowBase
    {
        public CustomerOrderWorkflow()
        {
            WorkflowSteps = new ISetupContext[]
            {
                // An activity
                new WorkflowActivitySetupContext
                    {
                        ActivityName = "VerifyOrder",
                        ActivityVersion = "1.0",
                        ActivityId = Guid.NewGuid().ToString(),
                        Control = "",
                        Input = "",
                        ScheduleToCloseTimeout = "60",
                        TaskList = "ActivityTaskList-Default",
                        ScheduleToStartTimeout = "60",
                        StartToCloseTimeout = "60",
                        HeartbeatTimeout = "NONE"
                    },
    
                // Followed by a child workflow
                new WorkflowSetupContext
                    {
                        TagList = new List<string>(),
                        WorkflowName = "VerifyCustomerWorkflow",
                        WorkflowVersion = "1.0",
                        WorkflowId = Guid.NewGuid().ToString(),
                        Control = "",
                        Input = "",
                        ExecutionStartToCloseTimeout = "600",
                        TaskList = "DeciderTaskList-Default",
                        TaskStartToCloseTimeout = "60",
                        ChildPolicy = "TERMINATE",
                    },
    
                // Followed by another activity
                new WorkflowActivitySetupContext
                    {
                        ActivityName = "ShipOrder",
                        ActivityVersion = "1.0",
                        ActivityId = Guid.NewGuid().ToString(),
                        Control = "",
                        Input = "",
                        ScheduleToCloseTimeout = "60",
                        TaskList = "ActivityTaskList-Default",
                        ScheduleToStartTimeout = "60",
                        StartToCloseTimeout = "60",
                        HeartbeatTimeout = "NONE"
                    }
            };
        }
    }

For detailed information, please refer to the code as that is really the best guide. 

### NuGet Package Information
A NuGet package is also available for this library. More information at 
https://nuget.org/packages/SimpleWorkflowFramework.NET/


### Contributors:
*   Shawn Debnath

There's a lot that can be done to improve this library and make other's lives easier and this project could definitely
use some help. I encourage folks to clone the repository, make improvements, be it code, features, documentation, 
tests ... anything. You can either send me a pull request or a diff/patch and I can add you as a collaborator. I will 
also be glad to add your name to the list of contributors.

If you would like to discuss a topic, feel free to email me at the link on https://github.com/sdebnath.
 
