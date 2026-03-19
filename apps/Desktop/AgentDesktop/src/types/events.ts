// Event types matching backend PlatformEvents.cs
export interface IPlatformEvent {
  timestamp: string;
}

export interface IntentReceivedEvent extends IPlatformEvent {
  intentId: string;
  userInput: string;
}

export interface TaskCreatedEvent extends IPlatformEvent {
  taskId: string;
  intentId: string;
  taskType: string;
  parameters: Record<string, any>;
}

export type PlatformEvent = IntentReceivedEvent | TaskCreatedEvent;

export interface EventStoreEntry {
  eventId: string;
  eventType: string;
  aggregateId: string;
  aggregateType: string;
  eventData: string;
  timestamp: string;
  userId?: string;
}
