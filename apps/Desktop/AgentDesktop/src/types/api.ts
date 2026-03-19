export interface ParsedIntent {
  type: string;
  confidence: number;
}
export interface SubmitIntentRequest {
  userInput: string;
}
export interface SubmitIntentResponse {
  intentId: string;
  parsed: ParsedIntent;
  timestamp: string;
}
