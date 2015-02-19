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
using System.Globalization;
using Amazon.SimpleWorkflow.Model;
using Newtonsoft.Json;

namespace SimpleWorkflowFramework.NET
{
    /// <summary>
    /// Workflow base class - provides an implementation for handling events that allows users to
    /// implement simple workflows without writing any code once the workflow steps are defined.
    /// </summary>
    public class WorkflowBase : IWorkflowDecisionMaker
    {
        #region Data Members

        protected ISetupContext[] WorkflowSteps;

        #endregion Data Members

        #region IWorkflowDecisionMaker Methods

        /// <summary>
        /// Sets up a new activity or child workflow as specified by the workflow at the start of the workflow.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnWorkflowExecutionStarted(WorkflowDecisionContext context)
        {
            var activityState = BuildActivityState(context);

            if (WorkflowSteps == null || WorkflowSteps.Length == 0)
            {
                return CompleteWorkflow("");
            }

            // Since this is the start of the workflow execution, we start off with the first item
            // in the workflow steps
            RespondDecisionTaskCompletedRequest decisionRequest;
            if (WorkflowSteps[0].IsActivity())
            {
                var activity = ((WorkflowActivitySetupContext) WorkflowSteps[0]).Clone();
                Debug.Assert(activity != null, "activity != null");

                // If input string is empty, we pass the input from the caller (in this case, workflow
                // execution start) to the activity
                if (String.IsNullOrEmpty(activity.Input))
                {
                    activity.Input = activityState;
                }

                decisionRequest = ScheduleActivityTask(activity);
            }
            else if (WorkflowSteps[0].IsTimer())
            {
                var timer = ((WorkflowTimerSetupContext) WorkflowSteps[0]).Clone();
                Debug.Assert(timer != null, "timer != null");

                decisionRequest = StartTimer(timer);
            }
            else if (WorkflowSteps[0].IsWorkflow())
            {
                var childWorkflow = ((WorkflowSetupContext) WorkflowSteps[0]).Clone();
                Debug.Assert(childWorkflow != null, "childWorkflow != null");

                // If input string is empty, we pass the input from the caller (in this case, workflow
                // execution start) to the activity
                if (String.IsNullOrEmpty(childWorkflow.Input))
                {
                    childWorkflow.Input = activityState;
                }

                decisionRequest = StartChildWorkflowExecution(childWorkflow);
            }
            else
            {
                throw new Exception("We can only have activity, timer, or workflow as workflow steps");
            }

            return decisionRequest;
        }

        /// <summary>
        /// Sets up a new activity or child workflow as specified by the workflow at the re-start of the workflow.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnWorkflowExecutionContinuedAsNew(
            WorkflowDecisionContext context)
        {
            return OnWorkflowExecutionStarted(context);
        }

        public virtual RespondDecisionTaskCompletedRequest OnWorkflowExecutionCancelRequested(WorkflowDecisionContext context)
        {
            return CancelWorkflow(context.Details);
        }

        /// <summary>
        /// Sets up a new activity or child workflow as specified by the workflow at the completion of an activity.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnActivityTaskCompleted(WorkflowDecisionContext context)
        {
            // Fetch the next step
            var nextStep = GetNextStep(true /* activity completion */, false /* is timer */, context.ActivityName, context.ActivityVersion);

            var activityState = BuildActivityState(context);
            return GetNextRequest(nextStep, context, activityState);
        }

        /// <summary>
        /// Set up a fail workflow decision in the event of an activity failing.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnActivityTaskFailed(WorkflowDecisionContext context)
        {
            return FailWorkflow(context.Details, context.Reason);
        }

        /// <summary>
        /// Re-tries the activity 3 times before failing the workflow.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnActivityTaskTimedOut(WorkflowDecisionContext context)
        {
            var timeoutCount = 0;
            var activityState = BuildActivityState(context);

            // If we have already re-tried 3 times, fail the workflow
            if (context.Markers.ContainsKey("ActivityTimeoutMarker"))
            {
                timeoutCount = Int32.Parse(context.Markers["ActivityTimeoutMarker"]);
                if (timeoutCount > 3)
                {
                    return FailWorkflow("Failing workflow after 3 retry attempts.", "OnActivityTaskTimedOut");
                }
            }

            // Bump the timeoutCount
            timeoutCount++;

            // Fetch the next step and set it up with the right input and increased timeout value
            var workflowStep = ((WorkflowActivitySetupContext) GetStep(true /* activity timed out */, false /* is timer */,
                                                                       context.ActivityName, context.ActivityVersion)).Clone();
            switch (context.TimeoutType)
            {
                case "START_TO_CLOSE":
                    workflowStep.StartToCloseTimeout =
                        (Int32.Parse(workflowStep.StartToCloseTimeout) * timeoutCount).ToString(CultureInfo.InvariantCulture);
                    break;

                case "SCHEDULE_TO_START":
                    workflowStep.ScheduleToStartTimeout =
                        (Int32.Parse(workflowStep.ScheduleToStartTimeout) * timeoutCount).ToString(CultureInfo.InvariantCulture);
                    break;

                case "SCHEDULE_TO_CLOSE":
                    workflowStep.ScheduleToCloseTimeout =
                        (Int32.Parse(workflowStep.ScheduleToCloseTimeout) * timeoutCount).ToString(CultureInfo.InvariantCulture);
                    break;

                case "HEARTBEAT":
                    workflowStep.HeartbeatTimeout =
                        (Int32.Parse(workflowStep.HeartbeatTimeout) * timeoutCount).ToString(CultureInfo.InvariantCulture);
                    break;

                default:
                    Debug.Assert(false, "Unknown timeout type " + context.TimeoutType);
                    break;
            }

            if (string.IsNullOrEmpty(workflowStep.Input))
            {
                workflowStep.Input = activityState;
            }

            // Fetch the decision to re-schedule the activity
            var request = ScheduleActivityTask(workflowStep);

            // Fetch the decision for a new timeout marker and add it to the schedule activity request
            var marker = RecordMarker(timeoutCount.ToString(CultureInfo.InvariantCulture), "ActivityTimeoutMarker");
            request.Decisions.Add(marker.Decisions[0]);

            // Return the combined request
            return request;
        }

        /// <summary>
        /// Set up a fail workflow decision in the event of a failure of scheduling a task.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnScheduleActivityTaskFailed(WorkflowDecisionContext context)
        {
            return FailWorkflow(context.Cause, "OnScheduleActivityTaskFailed");
        }

        /// <summary>
        /// Handles the child workflow execution started by returning an empty decision. This could be modified in the
        /// future to handle parallel child workflow/activity invocations.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnChildWorkflowExecutionStarted(WorkflowDecisionContext context)
        {
            return EmptyDecision();
        }

        /// <summary>
        /// Set up a fail workflow decision in the event of a failure of scheduling a task.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnChildWorkflowExecutionCompleted(WorkflowDecisionContext context)
        {
            // Fetch the next step
            var nextStep = GetNextStep(false /* workflow completion */, false /* is timer */,
                                       context.ChildWorkflowName, context.ChildWorkflowVersion);

            return GetNextRequest(nextStep, context, context.Result);
        }

        /// <summary>
        /// Set up a fail workflow decision in the event of a failure of a child workflow.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnChildWorkflowExecutionFailed(WorkflowDecisionContext context)
        {
            return FailWorkflow(context.Cause, "OnChildWorkflowExecutionFailed");
        }

        /// <summary>
        /// Set up a fail workflow decision in the event of termination of a child workflow.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnChildWorkflowExecutionTerminated(WorkflowDecisionContext context)
        {
            return FailWorkflow(context.Cause, "OnChildWorkflowExecutionTerminated");
        }

        /// <summary>
        /// Re-tries the child workflow 3 times before failing the workflow.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnChildWorkflowExecutionTimedOut(WorkflowDecisionContext context)
        {
            var timeoutCount = 0;

            // If we have already re-tried 3 times, fail the workflow
            if (context.Markers.ContainsKey("ChildWorkflowTimeoutMarker"))
            {
                timeoutCount = Int32.Parse(context.Markers["ChildWorkflowTimeoutMarker"]);
                if (timeoutCount > 3)
                {
                    return FailWorkflow("Failing workflow after 3 retry attempts.", "OnChildWorkflowExecutionTimedOut");
                }
            }
            
            // Fetch the next step and set it up with the right input and doubled timeout value
            var workflowStep = ((WorkflowSetupContext) GetStep(false /* workflow timed out */, false /* is timer */,
                                                               context.ChildWorkflowName, context.ChildWorkflowVersion)).Clone();
            switch (context.TimeoutType)
            {
                case "START_TO_CLOSE":
                    workflowStep.ExecutionStartToCloseTimeout =
                        (Int32.Parse(workflowStep.ExecutionStartToCloseTimeout) * 1 /*timeoutCount*/).ToString(CultureInfo.InvariantCulture);
                    break;

                default:
                    Debug.Assert(false, "Unknown timeout type " + context.TimeoutType);
                    break;
            }

            if (String.IsNullOrEmpty(workflowStep.Input))
            {
                workflowStep.Input = "";
            }
            
            // Fetch the decision to start the child workflow execution
            var request = StartChildWorkflowExecution(workflowStep);

            // Fetch the decision for a new timeout marker and add it to the child workflow request
            var marker = RecordMarker(timeoutCount.ToString(CultureInfo.InvariantCulture), "ChildWorkflowTimeoutMarker");
            request.Decisions.Add(marker.Decisions[0]);

            // Return the combined request
            return request;
        }

        /// <summary>
        /// Set up a fail workflow decision in the event of failure to start a child workflow.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest OnStartChildWorkflowExecutionFailed(WorkflowDecisionContext context)
        {
            return FailWorkflow(context.Cause, "OnStartChildWorkflowExecutionFailed");
        }

        public RespondDecisionTaskCompletedRequest OnTimerStarted(WorkflowDecisionContext context)
        {
            return EmptyDecision();
        }

        public RespondDecisionTaskCompletedRequest OnTimerFired(WorkflowDecisionContext context)
        {
            // Fetch the next step
            var nextStep = GetNextStep(false /* activity completion */, true /* is timer */, context.TimerId, null);

            var activityState = BuildActivityState(context);
            return GetNextRequest(nextStep, context, activityState);
        }

        public RespondDecisionTaskCompletedRequest OnTimerCanceled(WorkflowDecisionContext context)
        {
            var step = ((WorkflowTimerSetupContext) GetStep(false /* activity completion */, true /* is timer */,
                                                            context.TimerId, null /* step version */));

            switch (step.CancelAction)
            {
                case TimerCanceledAction.CompleteWorkflow:
                    return CompleteWorkflow(context.Result);

                case TimerCanceledAction.CancelWorkflow:
                    return CancelWorkflow(context.Details);

                case TimerCanceledAction.ProceedToNextActivity:
                    var nextStep = GetNextStep(false /* activity completion */, true /* is timer */, context.TimerId, null);

                    var activityState = BuildActivityState(context);
                    return GetNextRequest(nextStep, context, activityState);

                default:
                    throw new InvalidOperationException();
            }
        }

        private RespondDecisionTaskCompletedRequest GetNextRequest(ISetupContext nextStep, WorkflowDecisionContext context, string nextInput)
        {
            // If we don't have anymore steps, complete the workflow
            if (nextStep == null)
            {
                return CompleteWorkflow(context.Result);
            }

            // Found another step
            if (nextStep.IsActivity())
            {
                // Next step is an activity, set up a schedule activity decision
                var activity = ((WorkflowActivitySetupContext) nextStep).Clone();
                activity.Input = nextInput;
                return ScheduleActivityTask(activity);
            }

            if (nextStep.IsTimer())
            {
                // Next step is a timer, set up a start timer decision
                var timer = ((WorkflowTimerSetupContext) nextStep).Clone();
                return StartTimer(timer);
            }

            // Next step is not an activity, set up a child workflow decision
            Debug.Assert(nextStep.IsWorkflow(), "Steps can only be activities, timers, or workflows.");
            var workflow = ((WorkflowSetupContext) nextStep).Clone();
            workflow.Input = nextInput;
            return StartChildWorkflowExecution(workflow);
        }

        #endregion IWorkflowDecisionMaker Methods

        #region Decision Helpers

        /// <summary>
        /// Helper method to create an empty decision request.
        /// </summary>
        /// <returns>Properly set up decision completed request.</returns>
        public virtual RespondDecisionTaskCompletedRequest EmptyDecision()
        {
            var decisionRequest = new RespondDecisionTaskCompletedRequest {
                Decisions = new List<Decision>()
            };

            Debug.WriteLine(">>> Decision: <EMPTY>");
            return decisionRequest;
        }

        /// <summary>
        /// Builds the activity state object to pass to the activity.
        /// </summary>
        /// <param name="context">Workflow decision context supplied by SimpleWorkflowFramework.NET.</param>
        /// <returns>The activity state.</returns>
        protected string BuildActivityState(WorkflowDecisionContext context)
        {
            var activityState = new ActivityState {
                StartingInput = context.StartingInput,
                PreviousResult = context.Result
            };

            return JsonConvert.SerializeObject(activityState);
        }

        /// <summary>
        /// Helper method to schedule an activity task
        /// </summary>
        /// <param name="activityContext">Activity setup context.</param>
        /// <returns>Properly set up decision completed request.</returns>
        protected RespondDecisionTaskCompletedRequest ScheduleActivityTask(WorkflowActivitySetupContext activityContext)
        {
            var attributes = new ScheduleActivityTaskDecisionAttributes
            {
                ActivityId = activityContext.ActivityId,
                ActivityType = new ActivityType
                {
                    Name = activityContext.ActivityName,
                    Version = activityContext.ActivityVersion
                },
                Control = activityContext.Control,
                HeartbeatTimeout = activityContext.HeartbeatTimeout,
                Input = activityContext.Input,
                ScheduleToCloseTimeout = activityContext.ScheduleToCloseTimeout,
                ScheduleToStartTimeout = activityContext.ScheduleToStartTimeout,
                StartToCloseTimeout = activityContext.StartToCloseTimeout,
                TaskList = new TaskList
                    {
                        Name = activityContext.TaskList
                    }
            };

            var decisionRequest = new RespondDecisionTaskCompletedRequest
            {
                Decisions = new List<Decision>
                    {
                        new Decision
                            {
                                DecisionType = "ScheduleActivityTask",
                                ScheduleActivityTaskDecisionAttributes = attributes
                            }
                    }
            };

            Debug.WriteLine(">>> Decision: ScheduleActivityTask " +
                attributes.ActivityType.Name + " (" + attributes.ActivityType.Version + ")");
            return decisionRequest;
        }

        /// <summary>
        /// Helper method to create a new child workflow decision. 
        /// </summary>
        /// <param name="workflowContext">Workflow setup context.</param>
        /// <returns>Properly set up decision completed request.</returns>
        protected RespondDecisionTaskCompletedRequest StartChildWorkflowExecution(WorkflowSetupContext workflowContext)
        {
            var attributes = new StartChildWorkflowExecutionDecisionAttributes
            {
                WorkflowId = workflowContext.WorkflowId,
                WorkflowType = new WorkflowType
                {
                    Name = workflowContext.WorkflowName,
                    Version = workflowContext.WorkflowVersion
                },
                ChildPolicy = workflowContext.ChildPolicy,
                Control = workflowContext.Control,
                ExecutionStartToCloseTimeout = workflowContext.ExecutionStartToCloseTimeout,
                Input = workflowContext.Input,
                TagList = workflowContext.TagList,
                TaskList = new TaskList
                    {
                        Name = workflowContext.TaskList
                    },
                TaskStartToCloseTimeout = workflowContext.TaskStartToCloseTimeout,
            };

            var decisionRequest = new RespondDecisionTaskCompletedRequest
            {
                Decisions = new List<Decision>
                    {
                        new Decision
                            {
                                DecisionType = "StartChildWorkflowExecution",
                                StartChildWorkflowExecutionDecisionAttributes = attributes
                            }
                    }
            };

            Debug.WriteLine(">>> Decision: StartChildWorkflowExecution" +
                attributes.WorkflowType.Name + " (" + attributes.WorkflowType.Version + ")");
            return decisionRequest;
        }

        /// <summary>
        /// Helper method to create a complete workflow decision.
        /// </summary>
        /// <param name="result">Result of the activity execution.</param>
        /// <returns>Properly set up decision completed request.</returns>
        protected RespondDecisionTaskCompletedRequest CompleteWorkflow(string result)
        {
            var attributes = new CompleteWorkflowExecutionDecisionAttributes
            {
                Result = result
            };

            var decisionRequest = new RespondDecisionTaskCompletedRequest
            {
                Decisions = new List<Decision>
                    {
                        new Decision
                            {
                                DecisionType = "CompleteWorkflowExecution",
                                CompleteWorkflowExecutionDecisionAttributes = attributes
                            }
                    }
            };

            Debug.WriteLine(">>> Decision: CompleteWorkflowExecution");
            return decisionRequest;
        }

        /// <summary>
        /// Helper method to create a cancel workflow decision.
        /// </summary>
        /// <param name="details">Details for the cancellation.</param>
        /// <returns>Properly set up decision completed request.</returns>
        protected RespondDecisionTaskCompletedRequest CancelWorkflow(string details)
        {
            var attributes = new CancelWorkflowExecutionDecisionAttributes
            {
                Details = details
            };

            var decisionRequest = new RespondDecisionTaskCompletedRequest
            {
                Decisions = new List<Decision>
                    {
                        new Decision
                            {
                                DecisionType = "CancelWorkflowExecution",
                                CancelWorkflowExecutionDecisionAttributes = attributes 
                            }
                    }
            };

            Debug.WriteLine(">>> Decision: CancelWorkflowExecution");
            return decisionRequest;
        }

        /// <summary>
        /// Helper method to create a failed workflow decision.
        /// </summary>
        /// <param name="details">Failure details.</param>
        /// <param name="reason">Reason for the failure.</param>
        /// <returns>Properly set up decision completed request.</returns>
        protected RespondDecisionTaskCompletedRequest FailWorkflow(string details, string reason)
        {
            var attributes = new FailWorkflowExecutionDecisionAttributes
            {
                Details = details,
                Reason = reason
            };

            var decisionRequest = new RespondDecisionTaskCompletedRequest
            {
                Decisions = new List<Decision>
                    {
                        new Decision
                            {
                                DecisionType = "FailWorkflowExecution",
                                FailWorkflowExecutionDecisionAttributes = attributes 
                            }
                    }
            };

            Debug.WriteLine(">>> Decision: FailWorkflowExecution");
            return decisionRequest;
        }

        /// <summary>
        /// Helper method to create a record marker decision.
        /// </summary>
        /// <param name="details"></param>
        /// <param name="markerName"></param>
        /// <returns>Properly set up decision completed request.</returns>
        protected RespondDecisionTaskCompletedRequest RecordMarker(string details, string markerName)
        {
            var attributes = new RecordMarkerDecisionAttributes
            {
                Details = details,
                MarkerName = markerName
            };

            var decisionRequest = new RespondDecisionTaskCompletedRequest
            {
                Decisions = new List<Decision>
                    {
                        new Decision
                            {
                                DecisionType = "RecordMarker",
                                RecordMarkerDecisionAttributes = attributes
                            }
                    }
            };

            Debug.WriteLine(">>> Decision: RecordMarker [" +
                attributes.MarkerName + "] = " + attributes.Details);
            return decisionRequest;
        }

        protected RespondDecisionTaskCompletedRequest StartTimer(WorkflowTimerSetupContext timer)
        {
            var attributes = new StartTimerDecisionAttributes
            {
                TimerId = timer.TimerId,
                StartToFireTimeout = timer.StartToFireTimeoutInSeconds.ToString(),
                Control = timer.Control
            };

            var decisionRequest = new RespondDecisionTaskCompletedRequest
            {
                Decisions = new List<Decision>
                    {
                        new Decision
                            {
                                DecisionType = "StartTimer",
                                StartTimerDecisionAttributes = attributes
                            }
                    }
            };

            Debug.WriteLine(">>> Decision: StartTimer " + attributes.TimerId + " (elapses in " + attributes.StartToFireTimeout + " seconds)");

            return decisionRequest;
        }

        #endregion Decision Helpers

        #region Utility Methods

        /// <summary>
        /// Helper method to get the next step to schedule for execution.
        /// </summary>
        /// <param name="isActivity">Was the previous step an activity?</param>
        /// <param name="isTimer">Was the previous step a timer?</param>
        /// <param name="previousStepName">Previous step's name.</param>
        /// <param name="previousStepVersion">Previous step's version.</param>
        /// <returns>An ISetupContext if another step is found, otherwise null.</returns>
        protected ISetupContext GetNextStep(bool isActivity, bool isTimer, string previousStepName, string previousStepVersion)
        {
            for (var i = 0; i < WorkflowSteps.Length; i++)
            {
                var step = WorkflowSteps[i];
                Debug.Assert(step != null, "Null steps are not allowed.");

                if (step.IsActivity() && isActivity)
                {
                    // We are looking for an activity and the current step is an activity. Let's check to see if it 
                    // is the one we are looking for, if so, return the next step to execute
                    var activityContext = (WorkflowActivitySetupContext) step;
                    if (activityContext.ActivityName == previousStepName && activityContext.ActivityVersion == previousStepVersion)
                    {
                        if (i != (WorkflowSteps.Length - 1))
                        {
                            return WorkflowSteps[i + 1];
                        }
                    }
                }
                else if (step.IsTimer() & isTimer)
                {
                    var timerContext = (WorkflowTimerSetupContext) step;
                    if (timerContext.TimerId == previousStepName)
                    {
                        if (i != (WorkflowSteps.Length - 1))
                        {
                            return WorkflowSteps[i + 1];
                        }
                    }
                }
                else if (step.IsWorkflow() && !(isActivity || isTimer))
                {
                    // We are looking for a workflow and the current step is a workflow. Let's check to see if it 
                    // is the one we are looking for, if so, return the next step to execute
                    var workflowContext = (WorkflowSetupContext) step;
                    if (workflowContext.WorkflowName == previousStepName && workflowContext.WorkflowVersion == previousStepVersion)
                    {
                        if (i != (WorkflowSteps.Length - 1))
                        {
                            return WorkflowSteps[i + 1];
                        }
                    }
                }

                // Loop-de-loop
            }

            return null;
        }

        /// <summary>
        /// Helper method to locate a particular activity or workflow.
        /// </summary>
        /// <param name="isActivity">Was the previous step an activity?</param>
        /// <param name="isTimer">Was the previous step a timer?</param>
        /// <param name="stepName">Step's name.</param>
        /// <param name="stepVersion">Step's version.</param>
        /// <returns>An ISetupContext if another step is found, otherwise null.</returns>
        protected ISetupContext GetStep(bool isActivity, bool isTimer, string stepName, string stepVersion)
        {
            foreach (var step in WorkflowSteps)
            {
                if (step.IsActivity() && isActivity)
                {
                    // We are looking for an activity and the current step is an activity. Let's check to see if it 
                    // is the one we are looking for, if so, return it
                    var activityContext = (WorkflowActivitySetupContext) step;
                    if (activityContext.ActivityName == stepName && activityContext.ActivityVersion == stepVersion)
                    {
                        return step;
                    }
                }
                else if (step.IsTimer() & isTimer)
                {
                    var timerContext = (WorkflowTimerSetupContext) step;
                    if (timerContext.TimerId == stepName)
                    {
                        return step;
                    }
                }
                else if (step.IsWorkflow() && !(isActivity || isTimer))
                {
                    // We are looking for a workflow and the current step is a workflow. Let's check to see if it 
                    // is the one we are looking for, if so, return it
                    var workflowContext = (WorkflowSetupContext) step;
                    if (workflowContext.WorkflowName == stepName && workflowContext.WorkflowVersion == stepVersion)
                    {
                        return step;
                    }
                }
                else
                {
                    Debug.Assert(false, "Encountered a step that is neither activity nor workflow.");
                }
            }

            return null;
        }

        #endregion Utility Methods
    }
}
