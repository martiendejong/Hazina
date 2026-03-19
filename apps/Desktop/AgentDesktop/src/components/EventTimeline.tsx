import React, { useMemo } from 'react';
import { EventStoreEntry } from '../types/events';

interface EventTimelineProps {
  events: EventStoreEntry[];
  onEventClick?: (event: EventStoreEntry) => void;
  selectedEventId?: string | null;
}

export const EventTimeline: React.FC<EventTimelineProps> = ({
  events,
  onEventClick,
  selectedEventId,
}) => {
  const sortedEvents = useMemo(() => {
    return [...events].sort(
      (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
    );
  }, [events]);

  const getEventIcon = (eventType: string) => {
    if (eventType.includes('Intent')) return '💭';
    if (eventType.includes('Task')) return '📋';
    if (eventType.includes('Approval') || eventType.includes('Capability')) return '✋';
    if (eventType.includes('Indexation')) return '📑';
    if (eventType.includes('View')) return '👁️';
    return '📌';
  };

  const getEventColor = (eventType: string) => {
    if (eventType.includes('Failed') || eventType.includes('Denied')) return 'text-red-600';
    if (eventType.includes('Completed') || eventType.includes('Granted')) return 'text-green-600';
    if (eventType.includes('Started') || eventType.includes('Received')) return 'text-blue-600';
    return 'text-gray-600';
  };

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    return date.toLocaleTimeString('en-US', { hour12: false });
  };

  const formatEventData = (eventData: string) => {
    try {
      const data = JSON.parse(eventData);
      return JSON.stringify(data, null, 2);
    } catch {
      return eventData;
    }
  };

  return (
    <div className="event-timeline border rounded-lg shadow-md bg-white h-full flex flex-col">
      <div className="timeline-header p-4 border-b bg-gray-50">
        <h3 className="text-lg font-bold text-gray-800">Event Timeline</h3>
        <p className="text-xs text-gray-600 mt-1">{events.length} events</p>
      </div>

      <div className="timeline-content flex-1 overflow-y-auto p-4 space-y-2">
        {sortedEvents.length === 0 ? (
          <div className="text-center text-gray-500 py-8">
            <p className="text-4xl mb-2">🔮</p>
            <p>No events yet. Submit an intent to get started!</p>
          </div>
        ) : (
          sortedEvents.map((event) => (
            <div
              key={event.eventId}
              onClick={() => onEventClick?.(event)}
              className={`event-entry p-3 rounded-lg cursor-pointer transition ${
                selectedEventId === event.eventId
                  ? 'bg-blue-50 border-2 border-blue-500'
                  : 'bg-gray-50 hover:bg-gray-100 border border-gray-200'
              }`}
            >
              <div className="flex items-start gap-2">
                <span className="text-2xl">{getEventIcon(event.eventType)}</span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className={`font-semibold text-sm ${getEventColor(event.eventType)}`}>
                      {event.eventType}
                    </span>
                    <span className="text-xs text-gray-500">
                      {formatTimestamp(event.timestamp)}
                    </span>
                  </div>
                  {event.aggregateId && (
                    <div className="text-xs text-gray-600 mt-1">
                      <span className="font-mono bg-gray-200 px-1 rounded">
                        {event.aggregateId.substring(0, 8)}
                      </span>
                    </div>
                  )}
                  {selectedEventId === event.eventId && (
                    <div className="mt-2 p-2 bg-white rounded border text-xs font-mono overflow-x-auto">
                      <pre className="whitespace-pre-wrap">{formatEventData(event.eventData)}</pre>
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
};
