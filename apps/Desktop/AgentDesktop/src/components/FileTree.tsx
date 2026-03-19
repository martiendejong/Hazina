import React, { useState } from 'react';
import { FileTreeData, FileTreeNode } from '../types/schema';

interface FileTreeProps {
  data: FileTreeData;
  onFileClick?: (node: FileTreeNode) => void;
}

interface TreeNodeProps {
  node: FileTreeNode;
  level: number;
  onFileClick?: (node: FileTreeNode) => void;
}

const TreeNode: React.FC<TreeNodeProps> = ({ node, level, onFileClick }) => {
  const [isExpanded, setIsExpanded] = useState(level < 2);

  const handleClick = () => {
    if (node.type === 'directory') {
      setIsExpanded(!isExpanded);
    } else {
      onFileClick?.(node);
    }
  };

  const getIcon = () => {
    if (node.type === 'directory') {
      return isExpanded ? '📂' : '📁';
    }
    const ext = node.name.split('.').pop()?.toLowerCase();
    switch (ext) {
      case 'ts':
      case 'tsx':
      case 'js':
      case 'jsx':
        return '📜';
      case 'json':
        return '📋';
      case 'css':
      case 'scss':
        return '🎨';
      case 'md':
        return '📄';
      case 'png':
      case 'jpg':
      case 'svg':
        return '🖼️';
      default:
        return '📄';
    }
  };

  const formatSize = (bytes?: number) => {
    if (!bytes) return '';
    if (bytes < 1024) return `${bytes}B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)}MB`;
  };

  return (
    <div style={{ marginLeft: `${level * 16}px` }}>
      <div
        className="tree-node flex items-center gap-2 py-1 px-2 hover:bg-gray-100 rounded cursor-pointer"
        onClick={handleClick}
      >
        <span className="text-lg">{getIcon()}</span>
        <span className="flex-1 text-sm font-mono">{node.name}</span>
        {node.size && <span className="text-xs text-gray-500">{formatSize(node.size)}</span>}
        {node.type === 'directory' && node.children && (
          <span className="text-xs text-gray-400">({node.children.length})</span>
        )}
      </div>
      {node.type === 'directory' && isExpanded && node.children && (
        <div className="tree-children">
          {node.children.map((child, index) => (
            <TreeNode
              key={`${child.path}-${index}`}
              node={child}
              level={level + 1}
              onFileClick={onFileClick}
            />
          ))}
        </div>
      )}
    </div>
  );
};

export const FileTree: React.FC<FileTreeProps> = ({ data, onFileClick }) => {
  return (
    <div className="file-tree border rounded-lg shadow-md p-4 bg-white">
      <div className="file-tree-header mb-3">
        <h3 className="text-lg font-bold text-gray-800">File Explorer</h3>
        {data.filter && (
          <div className="text-xs text-gray-600 mt-1">
            {data.filter.extension && <span>Extension: {data.filter.extension}</span>}
            {data.filter.pattern && <span> | Pattern: {data.filter.pattern}</span>}
          </div>
        )}
      </div>
      <div className="file-tree-content max-h-96 overflow-y-auto">
        <TreeNode node={data.root} level={0} onFileClick={onFileClick} />
      </div>
    </div>
  );
};
