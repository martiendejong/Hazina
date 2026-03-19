// Event types matching backend PlatformEvents.cs
export interface IPlatformEvent {
  timestamp: string;
}

export interface IntentReceivedEvent extends IPlatformEvent {
  intentId: string;
  userInput: string;
}

export interface IntentParsedEvent extends IPlatformEvent {
  intentId: string;
  intentType: string;
  targetEntity?: string;
  filters?: Record<string, any>;
  confidence: number;
}

export interface TaskCreatedEvent extends IPlatformEvent {
  taskId: string;
  intentId: string;
  taskType: string;
  parameters: Record<string, any>;
}

export interface TaskStartedEvent extends IPlatformEvent {
  taskId: string;
}

export interface TaskCompletedEvent extends IPlatformEvent {
  taskId: string;
  result?: string;
  duration: string;
}

export interface TaskFailedEvent extends IPlatformEvent {
  taskId: string;
  errorMessage: string;
  stackTrace?: string;
}

export interface ViewOpenedEvent extends IPlatformEvent {
  viewId: string;
  componentId: string;
  taskId: string;
  viewData: any;
}

export interface ViewUpdatedEvent extends IPlatformEvent {
  viewId: string;
  viewData: any;
}

export interface ViewClosedEvent extends IPlatformEvent {
  viewId: string;
}

export interface ApprovalGrantedEvent extends IPlatformEvent {
  approvalId: string;
  taskId: string;
  actionType: string;
  actionTarget?: string;
}

export interface ApprovalDeniedEvent extends IPlatformEvent {
  approvalId: string;
  taskId: string;
  actionType: string;
  reason?: string;
}

export interface CapabilityRequestedEvent extends IPlatformEvent {
  requestId: string;
  taskId: string;
  capabilityType: string;
  context: string;
}

export interface CapabilityGrantedEvent extends IPlatformEvent {
  requestId: string;
  capabilityType: string;
  grantAlways: boolean;
}

export interface CapabilityDeniedEvent extends IPlatformEvent {
  requestId: string;
  capabilityType: string;
  reason?: string;
}

export interface IndexationStartedEvent extends IPlatformEvent {
  indexationId: string;
  rootPath: string;
  phase: string;
}

export interface IndexationProgressEvent extends IPlatformEvent {
  indexationId: string;
  filesScanned: number;
  filesIndexed: number;
  currentFile: string;
  percentComplete: number;
}

export interface IndexationCompletedEvent extends IPlatformEvent {
  indexationId: string;
  totalFiles: number;
  duration: string;
}

export type PlatformEvent =
  | IntentReceivedEvent
  | IntentParsedEvent
  | TaskCreatedEvent
  | TaskStartedEvent
  | TaskCompletedEvent
  | TaskFailedEvent
  | ViewOpenedEvent
  | ViewUpdatedEvent
  | ViewClosedEvent
  | ApprovalGrantedEvent
  | ApprovalDeniedEvent
  | CapabilityRequestedEvent
  | CapabilityGrantedEvent
  | CapabilityDeniedEvent
  | IndexationStartedEvent
  | IndexationProgressEvent
  | IndexationCompletedEvent;

export interface EventStoreEntry {
  eventId: string;
  eventType: string;
  aggregateId: string;
  aggregateType: string;
  eventData: string;
  timestamp: string;
  userId?: string;
  correlationId?: string;
  causationId?: string;
}
