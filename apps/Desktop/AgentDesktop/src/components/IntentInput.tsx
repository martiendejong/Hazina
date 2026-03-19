import React, { useState, FormEvent } from 'react';

interface IntentInputProps {
  onSubmit: (input: string) => Promise<void>;
  isLoading?: boolean;
  isConnected?: boolean;
}

export const IntentInput: React.FC<IntentInputProps> = ({
  onSubmit,
  isLoading = false,
  isConnected = true,
}) => {
  const [input, setInput] = useState('');
  const [suggestions] = useState([
    'list files in current directory',
    'show me Python files modified today',
    'analyze dependencies in package.json',
    'find all TODO comments',
  ]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!input.trim() || isLoading || !isConnected) return;

    await onSubmit(input.trim());
    setInput('');
  };

  const handleSuggestionClick = (suggestion: string) => {
    setInput(suggestion);
  };

  return (
    <div className="intent-input w-full">
      <form onSubmit={handleSubmit} className="flex flex-col gap-2">
        <div className="flex gap-2">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Enter your intent (e.g., 'list files in current directory')..."
            disabled={!isConnected || isLoading}
            className="flex-1 px-4 py-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-gray-100 disabled:cursor-not-allowed"
          />
          <button
            type="submit"
            disabled={!input.trim() || isLoading || !isConnected}
            className="px-6 py-3 bg-blue-500 text-white font-semibold rounded-lg hover:bg-blue-600 disabled:bg-gray-300 disabled:cursor-not-allowed transition"
          >
            {isLoading ? '⏳ Processing...' : '🚀 Submit'}
          </button>
        </div>

        {!isConnected && (
          <div className="text-sm text-red-600 flex items-center gap-2">
            <span>🔴</span>
            <span>Not connected to agent server</span>
          </div>
        )}

        <div className="suggestions flex flex-wrap gap-2">
          {suggestions.map((suggestion, index) => (
            <button
              key={index}
              type="button"
              onClick={() => handleSuggestionClick(suggestion)}
              disabled={!isConnected || isLoading}
              className="text-xs px-3 py-1.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-full transition disabled:opacity-50 disabled:cursor-not-allowed"
            >
              💡 {suggestion}
            </button>
          ))}
        </div>
      </form>
    </div>
  );
};
