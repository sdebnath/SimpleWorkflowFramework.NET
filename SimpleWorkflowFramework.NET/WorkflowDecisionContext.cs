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

using System.Collections.Generic;
using Amazon.SimpleWorkflow.Model;

namespace SimpleWorkflowFramework.NET
{
    /// <summary>
    /// Information required by workflow object to make a decision.
    /// </summary>
    public class WorkflowDecisionContext
    {
        public string DecisionType { get; set; }
		public string StartingInput { get; set; }
		public string ExecutionContext { get; set; }

        public string WorkflowName { get; set; }
        public string WorkflowVersion { get; set; }
        public string WorkflowId { get; set; }
        public string ActivityName { get; set; }
        public string ActivityVersion { get; set; }
        public string ChildWorkflowName { get; set; }
        public string ChildWorkflowVersion { get; set; }
		public string TimerId { get; set; }

        public Dictionary<string,string> Markers { get; set; }
		public Dictionary<string,TimerStartedEventAttributes> Timers { get; set; }
		public Dictionary<string,TimerFiredEventAttributes> FiredTimers { get; set; }
		public Dictionary<string,TimerCanceledEventAttributes> CanceledTimers { get; set; }

        public string Input { get; set; }
        public string Result { get; set; }
        public string Cause { get; set; }
        public string Details { get; set; }
        public string Reason { get; set; }
        public string Control { get; set; }
        public string TimeoutType { get; set; }
    
        public WorkflowDecisionContext()
        {
            Markers = new Dictionary<string, string>();
			Timers = new Dictionary<string, TimerStartedEventAttributes>();
			FiredTimers = new Dictionary<string, TimerFiredEventAttributes>();
			CanceledTimers = new Dictionary<string, TimerCanceledEventAttributes>();
        }
    }
}
