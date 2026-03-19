import { useEffect, useState } from 'react';
import { useAgentStore } from './store/agentStore';
import { useSignalR } from './hooks/useSignalR';
import { agentApi } from './services/agentApi';
import { schemaService } from './services/schemaService';
import { IntentInput } from './components/IntentInput';
import { EventTimeline } from './components/EventTimeline';
import { TaskCard } from './components/TaskCard';
import { FileTree } from './components/FileTree';
import './App.css';

function App() {
  const {
    events,
    isConnected,
    selectedEventId,
    setSelectedEventId,
    pendingApprovals,
    addTask,
  } = useAgentStore();

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [schemasLoaded, setSchemasLoaded] = useState(false);
  const [activeView, setActiveView] = useState<'timeline' | 'tasks' | 'files'>('timeline');

  // Connect to SignalR
  useSignalR();

  // Load schemas on mount
  useEffect(() => {
    schemaService
      .loadSchemas()
      .then(() => {
        setSchemasLoaded(true);
        console.log('Schemas loaded successfully');
      })
      .catch((error) => {
        console.error('Failed to load schemas:', error);
      });
  }, []);

  // Handle intent submission
  const handleIntentSubmit = async (input: string) => {
    setIsSubmitting(true);
    try {
      const response = await agentApi.submitIntent({ userInput: input });
      console.log('Intent submitted:', response);

      // Add task to store
      addTask({
        taskId: response.intentId,
        intentId: response.intentId,
        taskType: response.parsed.type,
        status: 'pending',
        parameters: response.parsed.filters || {},
        createdAt: response.timestamp,
      });
    } catch (error) {
      console.error('Failed to submit intent:', error);
      alert('Failed to submit intent. Make sure the agent server is running.');
    } finally {
      setIsSubmitting(false);
    }
  };

  // Example task card data (would come from event parsing)
  const exampleTaskCard = {
    title: 'List Python Files',
    description: 'Find all Python files modified today',
    steps: [
      {
        action: 'read' as const,
        target: '.',
        riskLevel: 'read' as const,
        description: 'Scan current directory',
      },
      {
        action: 'analyze' as const,
        target: '**/*.py',
        riskLevel: 'read' as const,
        description: 'Filter Python files',
      },
    ],
    estimatedDuration: 'PT5S',
    requiresApproval: false,
  };

  // Example file tree data
  const exampleFileTree = {
    root: {
      name: 'project',
      path: '/project',
      type: 'directory' as const,
      children: [
        {
          name: 'src',
          path: '/project/src',
          type: 'directory' as const,
          children: [
            { name: 'main.ts', path: '/project/src/main.ts', type: 'file' as const, size: 1234 },
            { name: 'App.tsx', path: '/project/src/App.tsx', type: 'file' as const, size: 5678 },
          ],
        },
        {
          name: 'package.json',
          path: '/project/package.json',
          type: 'file' as const,
          size: 890,
        },
      ],
    },
  };

  return (
    <div className="app h-screen flex flex-col bg-gray-100">
      {/* Header */}
      <header className="bg-white shadow-sm px-6 py-4 border-b">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-gray-800">Local Agent Platform</h1>
            <p className="text-sm text-gray-600">Milestone 1.1 - MVP</p>
          </div>
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2">
              <span
                className={`inline-block w-2 h-2 rounded-full ${
                  isConnected ? 'bg-green-500 animate-pulse' : 'bg-red-500'
                }`}
              ></span>
              <span className="text-sm text-gray-600">
                {isConnected ? 'Connected' : 'Disconnected'}
              </span>
            </div>
            {!schemasLoaded && (
              <span className="text-xs text-yellow-600">Loading schemas...</span>
            )}
          </div>
        </div>
      </header>

      {/* Main Content */}
      <div className="flex-1 flex flex-col gap-4 p-6 overflow-hidden">
        {/* Intent Input */}
        <div className="intent-section">
          <IntentInput
            onSubmit={handleIntentSubmit}
            isLoading={isSubmitting}
            isConnected={isConnected}
          />
        </div>

        {/* View Tabs */}
        <div className="tabs flex gap-2 border-b">
          <button
            onClick={() => setActiveView('timeline')}
            className={`px-4 py-2 font-semibold transition ${
              activeView === 'timeline'
                ? 'border-b-2 border-blue-500 text-blue-600'
                : 'text-gray-600 hover:text-gray-800'
            }`}
          >
            📊 Timeline ({events.length})
          </button>
          <button
            onClick={() => setActiveView('tasks')}
            className={`px-4 py-2 font-semibold transition ${
              activeView === 'tasks'
                ? 'border-b-2 border-blue-500 text-blue-600'
                : 'text-gray-600 hover:text-gray-800'
            }`}
          >
            📋 Tasks
          </button>
          <button
            onClick={() => setActiveView('files')}
            className={`px-4 py-2 font-semibold transition ${
              activeView === 'files'
                ? 'border-b-2 border-blue-500 text-blue-600'
                : 'text-gray-600 hover:text-gray-800'
            }`}
          >
            📁 Files
          </button>
          {pendingApprovals.length > 0 && (
            <div className="ml-auto flex items-center gap-2 px-4 py-2 bg-yellow-100 rounded text-yellow-800 text-sm font-semibold">
              ⚠️ {pendingApprovals.length} Pending Approval{pendingApprovals.length > 1 ? 's' : ''}
            </div>
          )}
        </div>

        {/* Content Area */}
        <div className="content-area flex-1 grid grid-cols-1 lg:grid-cols-2 gap-4 overflow-hidden">
          {/* Timeline View */}
          {activeView === 'timeline' && (
            <>
              <div className="h-full overflow-hidden">
                <EventTimeline
                  events={events}
                  onEventClick={(event) =>
                    setSelectedEventId(
                      selectedEventId === event.eventId ? null : event.eventId
                    )
                  }
                  selectedEventId={selectedEventId}
                />
              </div>
              <div className="h-full overflow-y-auto">
                <div className="text-center text-gray-500 py-8">
                  <p className="text-4xl mb-2">👁️</p>
                  <p>Schema-driven UI components will render here</p>
                  <p className="text-sm mt-2">Based on event data and component definitions</p>
                </div>
              </div>
            </>
          )}

          {/* Tasks View */}
          {activeView === 'tasks' && (
            <>
              <div className="h-full overflow-y-auto">
                <TaskCard data={exampleTaskCard} />
              </div>
              <div className="h-full overflow-y-auto">
                <div className="text-center text-gray-500 py-8">
                  <p className="text-4xl mb-2">📋</p>
                  <p>Active tasks from event store</p>
                </div>
              </div>
            </>
          )}

          {/* Files View */}
          {activeView === 'files' && (
            <>
              <div className="h-full overflow-y-auto">
                <FileTree data={exampleFileTree} />
              </div>
              <div className="h-full overflow-y-auto">
                <div className="text-center text-gray-500 py-8">
                  <p className="text-4xl mb-2">📂</p>
                  <p>File details and actions</p>
                </div>
              </div>
            </>
          )}
        </div>
      </div>

      {/* Footer */}
      <footer className="bg-white shadow-sm px-6 py-3 border-t text-center text-sm text-gray-600">
        <p>
          Hazina Local Agent Platform v1.1 | Events: {events.length} | Schemas:{' '}
          {schemasLoaded ? '✓' : '⏳'}
        </p>
      </footer>
    </div>
  );
}

export default App;
