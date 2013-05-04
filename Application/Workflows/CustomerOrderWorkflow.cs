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
using SimpleWorkflowFramework.NET;

namespace Application.Workflows
{
    /// <summary>
    /// A simple workflow that relies on WorkflowBase to do most of the event handling. All we do here is define 
    /// the activities and their order. If we wanted to handle our own events, we would simply need to override
    /// the callback methods defined in IWorkflowDecisionMaker that we want to add special case handling for. 
    /// </summary>
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
}
