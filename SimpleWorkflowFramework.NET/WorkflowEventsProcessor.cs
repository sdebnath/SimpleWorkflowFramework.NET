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
using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;

namespace SimpleWorkflowFramework.NET
{
    /// <summary>
    /// Processes SWF events to extract the context upon which the current decision needs to be made
    /// and calls on the appropriate workflow decision object to make a decision.
    /// </summary>
    public class WorkflowEventsProcessor
    {
        private readonly DecisionTask _decisionTask;
        private readonly PollForDecisionTaskRequest _request;
        private readonly WorkflowDecisionContext _decisionContext;
        private readonly IAmazonSimpleWorkflow _swfClient;
        private readonly WorkflowEventsIterator _events;
        private readonly Dictionary<string, Type> _workflows;

        /// <summary>
        /// Constructor for the workflow event processor. 
        /// </summary>
        /// <param name="decisionTask">Decision task passed in from SWF as decision task response.</param>
        /// <param name="workflows">IEnumerable set of string for workflow name and Type for workflow class.</param>
        /// <param name="request">The request used to retrieve <paramref name="decisionTask"/>, which will be used to retrieve subsequent history event pages.</param>
        /// <param name="swfClient">An SWF client.</param>
        public WorkflowEventsProcessor(DecisionTask decisionTask, IEnumerable<KeyValuePair<string, Type>> workflows, PollForDecisionTaskRequest request, IAmazonSimpleWorkflow swfClient)
        {
            // Decision task can't be null.
            if (decisionTask == null)
            {
                throw new ArgumentNullException("decisionTask");
            }

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            // Store the decision task and allocate a new decision context and event dictionary which
            // we will use as we walk through the chain of events
            _decisionTask = decisionTask;
            _request = request;
            _decisionContext = new WorkflowDecisionContext();
            _swfClient = swfClient;

            // Set up our events data structure
            _events = new WorkflowEventsIterator(ref decisionTask, _request, _swfClient);
            _workflows = (Dictionary<string, Type>) workflows;
        }

        /// <summary>
        /// Walks through the relevant part of the event history chain and populates the decision context. Then calls
        /// on the appropriate workflow decision object to make a decision.
        /// </summary>
        /// <returns>Decision on how to proceed.</returns>
        public RespondDecisionTaskCompletedRequest Decide()
        {
            RespondDecisionTaskCompletedRequest decisionCompletedRequest;

            // Step 1: Walk through the relevant part of the event history chain and populate the decision context

            // Retrieve and store the workflow information
            _decisionContext.WorkflowName = _decisionTask.WorkflowType.Name;
            _decisionContext.WorkflowVersion = _decisionTask.WorkflowType.Version;
            _decisionContext.WorkflowId = _decisionTask.WorkflowExecution.WorkflowId;
            
            // Walk through the chain of events based on event ID to identify what we need to decide on
            Debug.WriteLine(">>> Workflow: " + _decisionContext.WorkflowName);
            foreach (var historyEvent in _events)
            {
                Debug.WriteLine(">>> Event Type [" + historyEvent.EventId + "] " + historyEvent.EventType);
                switch (historyEvent.EventType)
                {
                    case "WorkflowExecutionStarted":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.Input = historyEvent.WorkflowExecutionStartedEventAttributes.Input;
                        _decisionContext.StartingInput = historyEvent.WorkflowExecutionStartedEventAttributes.Input;
                        break;

                    case "WorkflowExecutionContinuedAsNew":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.Input = historyEvent.WorkflowExecutionContinuedAsNewEventAttributes.Input;
                        break;

                    case "WorkflowExecutionCancelRequested":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.Cause = historyEvent.WorkflowExecutionCancelRequestedEventAttributes.Cause;
                        break;

                    case "DecisionTaskCompleted":
                        // If a decision task completed event was encountered, use it to save 
                        // some of the key information as the execution context is not available as part of
                        // the rest of the ActivityTask* event attributes.
                        // NB: We don't act on this event.
                        _decisionContext.ExecutionContext = historyEvent.DecisionTaskCompletedEventAttributes.ExecutionContext;
                        break;

                    case "ActivityTaskScheduled":
                        // If an activity task scheduled event was encountered, use it to save 
                        // some of the key information as the activity information is not available as part of
                        // the rest of the ActivityTask* event attributes. We don't act on this event.
                        _decisionContext.ActivityName =
                            historyEvent.ActivityTaskScheduledEventAttributes.ActivityType.Name;
                        _decisionContext.ActivityVersion =
                            historyEvent.ActivityTaskScheduledEventAttributes.ActivityType.Version;
                        _decisionContext.Control = historyEvent.ActivityTaskScheduledEventAttributes.Control;
                        _decisionContext.Input = historyEvent.ActivityTaskScheduledEventAttributes.Input;
                        break;

                    case "ActivityTaskCompleted":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.Result = historyEvent.ActivityTaskCompletedEventAttributes.Result;
                        break;

                    case "ActivityTaskFailed":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.Details = historyEvent.ActivityTaskFailedEventAttributes.Details;
                        _decisionContext.Reason = historyEvent.ActivityTaskFailedEventAttributes.Reason;
                        break;

                    case "ActivityTaskTimedOut":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.Details = historyEvent.ActivityTaskTimedOutEventAttributes.Details;
                        _decisionContext.TimeoutType = historyEvent.ActivityTaskTimedOutEventAttributes.TimeoutType;
                        break;

                    case "ScheduleActivityTaskFailed":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.ActivityName =
                            historyEvent.ScheduleActivityTaskFailedEventAttributes.ActivityType.Name;
                        _decisionContext.ActivityVersion =
                            historyEvent.ScheduleActivityTaskFailedEventAttributes.ActivityType.Version;
                        _decisionContext.Cause = historyEvent.ScheduleActivityTaskFailedEventAttributes.Cause;
                        break;

                    case "ChildWorkflowExecutionStarted":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.ChildWorkflowName =
                            historyEvent.ChildWorkflowExecutionStartedEventAttributes.WorkflowType.Name;
                        _decisionContext.ChildWorkflowVersion =
                            historyEvent.ChildWorkflowExecutionStartedEventAttributes.WorkflowType.Version;
                        break;

                    case "ChildWorkflowExecutionCompleted":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.ChildWorkflowName =
                            historyEvent.ChildWorkflowExecutionCompletedEventAttributes.WorkflowType.Name;
                        _decisionContext.ChildWorkflowVersion =
                            historyEvent.ChildWorkflowExecutionCompletedEventAttributes.WorkflowType.Version;
                        _decisionContext.Result = historyEvent.ChildWorkflowExecutionCompletedEventAttributes.Result;
                        break;

                    case "ChildWorkflowExecutionFailed":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.ChildWorkflowName =
                            historyEvent.ChildWorkflowExecutionFailedEventAttributes.WorkflowType.Name;
                        _decisionContext.ChildWorkflowVersion =
                            historyEvent.ChildWorkflowExecutionFailedEventAttributes.WorkflowType.Version;
                        _decisionContext.Details = historyEvent.ChildWorkflowExecutionFailedEventAttributes.Details;
                        _decisionContext.Reason = historyEvent.ChildWorkflowExecutionFailedEventAttributes.Reason;
                        break;

                    case "ChildWorkflowExecutionTerminated":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.ChildWorkflowName =
                            historyEvent.ChildWorkflowExecutionTerminatedEventAttributes.WorkflowType.Name;
                        _decisionContext.ChildWorkflowVersion =
                            historyEvent.ChildWorkflowExecutionTerminatedEventAttributes.WorkflowType.Version;
                        break;

                    case "ChildWorkflowExecutionTimedOut":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.ChildWorkflowName =
                            historyEvent.ChildWorkflowExecutionTimedOutEventAttributes.WorkflowType.Name;
                        _decisionContext.ChildWorkflowVersion =
                            historyEvent.ChildWorkflowExecutionTimedOutEventAttributes.WorkflowType.Version;
                        _decisionContext.TimeoutType =
                            historyEvent.ChildWorkflowExecutionTimedOutEventAttributes.TimeoutType;
                        break;

                    case "MarkerRecorded":
                        // We don't act on markers but save the marker information in the decision context so that
                        // the workflow has all the information it needs to make the decision. NOTE: values of markers
                        // with the same names are overwritten
                        var markerName = historyEvent.MarkerRecordedEventAttributes.MarkerName;
                        _decisionContext.Markers[markerName] = historyEvent.MarkerRecordedEventAttributes.Details;
                        Debug.WriteLine(">>> Marker [" + markerName + "] = " + _decisionContext.Markers[markerName]);
                        break;

                    case "StartChildWorkflowExecutionFailed":
                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.ChildWorkflowName =
                            historyEvent.StartChildWorkflowExecutionFailedEventAttributes.WorkflowType.Name;
                        _decisionContext.ChildWorkflowVersion =
                            historyEvent.StartChildWorkflowExecutionFailedEventAttributes.WorkflowType.Version;
                        _decisionContext.Cause = historyEvent.StartChildWorkflowExecutionFailedEventAttributes.Cause;
                        break;

                    case "TimerStarted":
                        var timer = historyEvent.TimerStartedEventAttributes;

                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.TimerId = timer.TimerId;
                        _decisionContext.Timers[timer.TimerId] = timer;
                        break;

                    case "TimerFired":
                        var firedTimer = historyEvent.TimerFiredEventAttributes;

                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.TimerId = firedTimer.TimerId;

                        if (_decisionContext.Timers.ContainsKey(firedTimer.TimerId))
                        {
                            _decisionContext.FiredTimers[firedTimer.TimerId] = firedTimer;
                            _decisionContext.Timers.Remove(firedTimer.TimerId);
                        }

                        break;

                    case "TimerCanceled":
                        var canceledTimer = historyEvent.TimerCanceledEventAttributes;

                        _decisionContext.DecisionType = historyEvent.EventType;
                        _decisionContext.TimerId = canceledTimer.TimerId;

                        if (_decisionContext.Timers.ContainsKey(canceledTimer.TimerId))
                        {
                            _decisionContext.CanceledTimers[canceledTimer.TimerId] = canceledTimer;
                            _decisionContext.Timers.Remove(canceledTimer.TimerId);
                        }

                        break;
                }
            }

            // Step 2: decide on what to do based on the processed events

            // Create the correct instance of the decision maker
            var decisionMaker = 
                (IWorkflowDecisionMaker) Activator.CreateInstance(_workflows[_decisionContext.WorkflowName]);

            // Match the context and call the right method to make a decision
            switch (_decisionContext.DecisionType)
            {
                case "WorkflowExecutionStarted":
                    decisionCompletedRequest = decisionMaker.OnWorkflowExecutionStarted(_decisionContext);
                    break;

                case "WorkflowExecutionContinuedAsNew":
                    decisionCompletedRequest = decisionMaker.OnWorkflowExecutionContinuedAsNew(_decisionContext);                
                    break;

                case "WorkflowExecutionCancelRequested":
                    decisionCompletedRequest = decisionMaker.OnWorkflowExecutionCancelRequested(_decisionContext);
                    break;

                case "ActivityTaskCompleted":
                    decisionCompletedRequest = decisionMaker.OnActivityTaskCompleted(_decisionContext);
                    break;

                case "ActivityTaskFailed":
                    decisionCompletedRequest = decisionMaker.OnActivityTaskFailed(_decisionContext);
                    break;

                case "ActivityTaskTimedOut":
                    decisionCompletedRequest = decisionMaker.OnActivityTaskTimedOut(_decisionContext);
                    break;

                case "ScheduleActivityTaskFailed":
                    decisionCompletedRequest = decisionMaker.OnScheduleActivityTaskFailed(_decisionContext);
                    break;

                case "ChildWorkflowExecutionStarted":
                    decisionCompletedRequest = decisionMaker.OnChildWorkflowExecutionStarted(_decisionContext);
                    break;

                case "ChildWorkflowExecutionCompleted":
                    decisionCompletedRequest = decisionMaker.OnChildWorkflowExecutionCompleted(_decisionContext);
                    break;

                case "ChildWorkflowExecutionFailed":
                    decisionCompletedRequest = decisionMaker.OnChildWorkflowExecutionFailed(_decisionContext);
                    break;

                case "ChildWorkflowExecutionTerminated":
                    decisionCompletedRequest = decisionMaker.OnChildWorkflowExecutionTerminated(_decisionContext);
                    break;

                case "ChildWorkflowExecutionTimedOut":
                    decisionCompletedRequest = decisionMaker.OnChildWorkflowExecutionTimedOut(_decisionContext);
                    break;

                case "StartChildWorkflowExecutionFailed":
                    decisionCompletedRequest = decisionMaker.OnStartChildWorkflowExecutionFailed(_decisionContext);
                    break;

                case "TimerStarted":
                    decisionCompletedRequest = decisionMaker.OnTimerStarted(_decisionContext);
                    break;

                case "TimerFired":
                    decisionCompletedRequest = decisionMaker.OnTimerFired(_decisionContext);
                    break;

                case "TimerCanceled":
                    decisionCompletedRequest = decisionMaker.OnTimerCanceled(_decisionContext);
                    break;

                default:
                    throw new InvalidOperationException("Unhandled event type.");
            }

            // Assign the task token and return
            decisionCompletedRequest.TaskToken = _decisionTask.TaskToken;
            decisionCompletedRequest.ExecutionContext = _decisionContext.ExecutionContext;
            return decisionCompletedRequest;
        }
    }
}
