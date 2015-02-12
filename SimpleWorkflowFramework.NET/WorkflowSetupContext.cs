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

namespace SimpleWorkflowFramework.NET
{
    /// <summary>
    /// Information required to initiate a workflow.
    /// </summary>
    [Serializable]
    public class WorkflowSetupContext : ISetupContext
    {
        public string WorkflowName { get; set; }
        public string WorkflowVersion { get; set; }
        public string WorkflowId { get; set; }
        public string ChildPolicy { get; set; }
        public string Control { get; set; }

        // If the input field is an empty string, the result from the previous activity
        // or child workflow execution is provided as the input.
        public string Input { get; set; }

        public string ExecutionStartToCloseTimeout { get; set; }
        public List<string> TagList { get; set; }
        public string TaskList { get; set; }
        public string TaskStartToCloseTimeout { get; set; }

        // ISetupContext members
        public bool IsActivity() { return false; }
        public bool IsWorkflow() { return true; }
		public bool IsTimer() { return false; }
    }
}
