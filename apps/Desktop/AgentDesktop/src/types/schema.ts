// Minimal schema types
export interface TaskCardData {
  title: string;
  steps: Array<{action: string; target: string; risk Level?: string}>;
}
export interface FileTreeData {
  root: {name: string; path: string; type: string; children?: any[]};
}
