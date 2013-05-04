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
using Amazon.SimpleWorkflow.Model;

namespace SimpleWorkflowFramework.NET
{
    /// <summary>
    /// Provides an iterator for the history events in a decision request.
    /// </summary>
    public class WorkflowEventsIterator
    {
        private readonly List<HistoryEvent> _historyEvents; 
        private readonly int _scopedStartEventId;
        private readonly int _scopedEndEventId;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="decisionTask">Reference to the decision task passed in from SWF.</param>
        public WorkflowEventsIterator(ref DecisionTask decisionTask)
        {
            // Store a reference to the events
            _historyEvents = decisionTask.Events;

            // Store the start and end event IDs
            _scopedStartEventId = (int)decisionTask.PreviousStartedEventId == 0
                                ? 1
                                : (int)decisionTask.PreviousStartedEventId;
            _scopedEndEventId = decisionTask.Events.Count;
        }

        /// <summary>
        /// Scoped start event ID for the decision to be made.
        /// </summary>
        public int ScopedStartEventId
        {
            get { return _scopedStartEventId; }
        }

        /// <summary>
        /// Scoped end event ID for the decision to be made.
        /// </summary>
        public int ScopedEndEventId
        {
            get { return _scopedEndEventId; }
        }

        /// <summary>
        /// Enumerator for the scoped events needed for the decision.
        /// </summary>
        /// <returns>IEnumerator for the scoped events.</returns>
        public IEnumerator<HistoryEvent> GetEnumerator()
        {
            for (var i = _scopedStartEventId; i <= _scopedEndEventId; i++)
            {
                yield return _historyEvents[i - 1];
            }
        }

        /// <summary>
        /// Indexer access based on event ID.
        /// </summary>
        /// <param name="eventId">Event ID.</param>
        /// <returns>HistoryEvent.</returns>
        public HistoryEvent this[int eventId]
        {
            get
            {
                // In the case of a brand new workflow, since no previous decisions were started, the 
                // PreviousStartedEventId in the decision task is set to 0. If using the iterator, folks should 
                // not try to access event directly but instead through us. So for the following conditions, throw
                // argument out of range exception
                if (eventId == 0 || eventId < _scopedStartEventId || eventId > _scopedEndEventId)
                {
                    throw new ArgumentOutOfRangeException("eventId");
                }

                return _historyEvents[eventId - 1];
            }
        }
    }
}
