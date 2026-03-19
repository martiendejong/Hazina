import * as signalR from '@microsoft/signalr';
class SignalRService {
  connection: signalR.HubConnection | null = null;
  async connect() {}
  onEvent(cb: any): () => void { return () => {}; }
  onConnectionChange(cb: any): () => void { return () => {}; }
}
export const signalRService = new SignalRService();
export default signalRService;
