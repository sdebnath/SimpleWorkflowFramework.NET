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
using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;

namespace SimpleWorkflowFramework.NET
{
    /// <summary>
    /// Provides an iterator for the history events in a decision request.
    /// </summary>
    public class WorkflowEventsIterator : IEnumerable<HistoryEvent>
    {
        private readonly List<HistoryEvent> _historyEvents;
        private readonly PollForDecisionTaskRequest _request;
        private readonly IAmazonSimpleWorkflow _swfClient;
        private DecisionTask _lastResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleWorkflowFramework.NET.WorkflowEventsIterator"/> class.
        /// </summary>
        /// <param name="decisionTask">Reference to the decision task passed in from SWF.</param>
        /// <param name="request">The request used to retrieve <paramref name="decisionTask"/>, which will be used to retrieve subsequent history event pages.</param>
        /// <param name="swfClient">An SWF client.</param>
        public WorkflowEventsIterator(ref DecisionTask decisionTask, PollForDecisionTaskRequest request, IAmazonSimpleWorkflow swfClient)
        {
            _lastResponse = decisionTask;
            _request = request;
            _swfClient = swfClient;

            _historyEvents = decisionTask.Events;
        }

        /// <summary>
        /// Enumerator for history events needed for the decision.
        /// </summary>
        /// <returns>IEnumerator for the scoped events.</returns>
        public IEnumerator<HistoryEvent> GetEnumerator()
        {
            foreach (HistoryEvent e in _historyEvents)
            {
                yield return e;
            }

            while (!string.IsNullOrEmpty(_lastResponse.NextPageToken))
            {
                var events = GetNextPage();
                _historyEvents.AddRange(events);

                foreach (HistoryEvent e in events)
                {
                    yield return e;
                }
            }
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
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
                // While the eventId is not in range and there are more history pages to retrieve,
                // retrieve more history events.
                while (eventId != 0 && eventId > _historyEvents.Count && !string.IsNullOrEmpty(_lastResponse.NextPageToken))
                {
                    var events = GetNextPage();
                    _historyEvents.AddRange(events);
                }

                if (eventId < 0 || eventId > _historyEvents.Count)
                {
                    throw new ArgumentOutOfRangeException("eventId");
                }

                return _historyEvents[eventId - 1];
            }
        }

        /// <summary>
        /// Retrieves the next page of history from SWF.
        /// </summary>
        /// <returns>The next page of history events.</returns>
        private List<HistoryEvent> GetNextPage()
        {
            var request = new PollForDecisionTaskRequest {
                Domain = _request.Domain,
                NextPageToken = _lastResponse.NextPageToken,
                TaskList = _request.TaskList,
                MaximumPageSize = _request.MaximumPageSize
            };

            const int retryCount = 10;
            int currentTry = 1;
            bool pollFailed;

            do
            {
                pollFailed = false;

                try
                {
                    _lastResponse = _swfClient.PollForDecisionTask(request).DecisionTask;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Poll request failed with exception: " + ex);
                    pollFailed = true;
                }

                currentTry += 1;
            } while (pollFailed && currentTry <= retryCount);

            return _lastResponse.Events;
        }
    }
}
