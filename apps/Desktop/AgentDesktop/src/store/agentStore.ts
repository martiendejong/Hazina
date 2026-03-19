import { create } from 'zustand';
import {
  PlatformEvent,
  EventStoreEntry,
  IntentReceivedEvent,
  TaskCreatedEvent,
  CapabilityRequestedEvent,
} from '../types/events';
import { TaskInfo, ApprovalRequest } from '../types/api';

interface AgentState {
  // Events
  events: EventStoreEntry[];
  addEvent: (event: EventStoreEntry) => void;
  clearEvents: () => void;

  // Tasks
  tasks: Map<string, TaskInfo>;
  addTask: (task: TaskInfo) => void;
  updateTask: (taskId: string, updates: Partial<TaskInfo>) => void;
  getTask: (taskId: string) => TaskInfo | undefined;

  // Active intent
  currentIntent: string | null;
  setCurrentIntent: (intentId: string | null) => void;

  // Pending approvals
  pendingApprovals: ApprovalRequest[];
  addPendingApproval: (approval: ApprovalRequest) => void;
  removePendingApproval: (requestId: string) => void;

  // Connection status
  isConnected: boolean;
  setConnected: (connected: boolean) => void;

  // UI state
  selectedEventId: string | null;
  setSelectedEventId: (eventId: string | null) => void;
}

export const useAgentStore = create<AgentState>((set, get) => ({
  // Events
  events: [],
  addEvent: (event) =>
    set((state) => ({
      events: [...state.events, event],
    })),
  clearEvents: () => set({ events: [] }),

  // Tasks
  tasks: new Map(),
  addTask: (task) =>
    set((state) => {
      const newTasks = new Map(state.tasks);
      newTasks.set(task.taskId, task);
      return { tasks: newTasks };
    }),
  updateTask: (taskId, updates) =>
    set((state) => {
      const newTasks = new Map(state.tasks);
      const existing = newTasks.get(taskId);
      if (existing) {
        newTasks.set(taskId, { ...existing, ...updates });
      }
      return { tasks: newTasks };
    }),
  getTask: (taskId) => get().tasks.get(taskId),

  // Active intent
  currentIntent: null,
  setCurrentIntent: (intentId) => set({ currentIntent: intentId }),

  // Pending approvals
  pendingApprovals: [],
  addPendingApproval: (approval) =>
    set((state) => ({
      pendingApprovals: [...state.pendingApprovals, approval],
    })),
  removePendingApproval: (requestId) =>
    set((state) => ({
      pendingApprovals: state.pendingApprovals.filter((a) => a.requestId !== requestId),
    })),

  // Connection status
  isConnected: false,
  setConnected: (connected) => set({ isConnected: connected }),

  // UI state
  selectedEventId: null,
  setSelectedEventId: (eventId) => set({ selectedEventId: eventId }),
}));
