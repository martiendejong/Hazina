import axios, { AxiosInstance } from 'axios';
import {
  SubmitIntentRequest,
  SubmitIntentResponse,
  GetTasksResponse,
  ApproveActionRequest,
  ApproveActionResponse,
} from '../types/api';
import { EventStoreEntry } from '../types/events';

class AgentApiService {
  private api: AxiosInstance;
  private baseURL: string;

  constructor(baseURL: string = 'http://localhost:5000') {
    this.baseURL = baseURL;
    this.api = axios.create({
      baseURL: `${baseURL}/api`,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    });
  }

  // Submit user intent
  async submitIntent(request: SubmitIntentRequest): Promise<SubmitIntentResponse> {
    const response = await this.api.post<SubmitIntentResponse>('/agent/intent', request);
    return response.data;
  }

  // Get all tasks
  async getTasks(): Promise<GetTasksResponse> {
    const response = await this.api.get<GetTasksResponse>('/agent/tasks');
    return response.data;
  }

  // Get events from event store
  async getEvents(
    limit?: number,
    offset?: number,
    aggregateId?: string
  ): Promise<EventStoreEntry[]> {
    const response = await this.api.get<EventStoreEntry[]>('/agent/events', {
      params: { limit, offset, aggregateId },
    });
    return response.data;
  }

  // Approve/deny action
  async approveAction(request: ApproveActionRequest): Promise<ApproveActionResponse> {
    const response = await this.api.post<ApproveActionResponse>('/agent/approve', request);
    return response.data;
  }

  // Get file tree from structural indexer
  async getFileTree(rootPath?: string): Promise<any> {
    const response = await this.api.get('/agent/files', {
      params: { rootPath },
    });
    return response.data;
  }

  // Health check
  async healthCheck(): Promise<boolean> {
    try {
      await this.api.get('/health');
      return true;
    } catch {
      return false;
    }
  }

  getBaseURL(): string {
    return this.baseURL;
  }
}

export const agentApi = new AgentApiService();
export default agentApi;
