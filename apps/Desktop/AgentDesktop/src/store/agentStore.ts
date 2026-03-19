import { create } from 'zustand';
import { EventStoreEntry } from '../types/events';
export const useAgentStore = create<{
  events: EventStoreEntry[];
  isConnected: boolean;
  addEvent: (e: EventStoreEntry) => void;
  setConnected: (c: boolean) => void;
}>((set) => ({
  events: [],
  isConnected: false,
  addEvent: (event) => set((s) => ({ events: [...s.events, event] })),
  setConnected: (connected) => set({ isConnected: connected }),
}));
