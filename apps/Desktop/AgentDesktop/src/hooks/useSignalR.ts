import { useEffect } from 'react';
import { useAgentStore } from '../store/agentStore';
import { signalRService } from '../services/signalRService';

export function useSignalR(baseURL?: string) {
  const { addEvent, setConnected } = useAgentStore();

  useEffect(() => {
    // Connect to SignalR
    signalRService.connect(baseURL).catch((error) => {
      console.error('Failed to connect to SignalR:', error);
    });

    // Subscribe to events
    const unsubscribeEvents = signalRService.onEvent((event) => {
      addEvent(event);
    });

    // Subscribe to connection state
    const unsubscribeConnection = signalRService.onConnectionChange((connected) => {
      setConnected(connected);
    });

    // Cleanup on unmount
    return () => {
      unsubscribeEvents();
      unsubscribeConnection();
      signalRService.disconnect();
    };
  }, [baseURL, addEvent, setConnected]);
}
