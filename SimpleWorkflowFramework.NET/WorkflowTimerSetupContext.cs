//----------------------------------------------------------------------------------------------------------------------
//  Copyright (c) 2015, El Loco. All rights reserved.
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

namespace SimpleWorkflowFramework.NET
{
    public enum TimerCanceledAction
    {
        ProceedToNext,
        CancelWorkflow,
        CompleteWorkflow
    }

    [Serializable]
    public class WorkflowTimerSetupContext : ISetupContext
    {
        public string TimerId { get; set; }
        public string StartToFileTimeout { get; set; }
        public string Control { get; set; }

        private TimerCanceledAction _cancelAction = TimerCanceledAction.ProceedToNext;
        public TimerCanceledAction CancelAction {
            get { return _cancelAction; }
            set { _cancelAction = value; }
        }

        public delegate string OnCancel(WorkflowDecisionContext context);

        public bool IsActivity() { return false; }
        public bool IsWorkflow() { return false; }
        public bool IsTimer() { return true; }
    }
}

