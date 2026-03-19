import axios from 'axios';
const api = axios.create({ baseURL: 'http://localhost:5000/api' });
export const agentApi = {
  submitIntent: (req: any) => api.post('/agent/intent', req).then(r => r.data),
  getEvents: () => api.get('/agent/events').then(r => r.data),
};
export default agentApi;
