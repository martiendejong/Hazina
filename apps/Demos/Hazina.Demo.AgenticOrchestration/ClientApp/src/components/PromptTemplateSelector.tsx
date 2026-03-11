import { useState, useEffect } from 'react';

interface PromptTemplate {
    id: string;
    name: string;
    description?: string;
    promptText: string;
    category?: string;
    icon?: string;
    isFavorite: boolean;
    usageCount: number;
    autoExecute: boolean;
}

interface PromptTemplateSelectorProps {
    onSelectTemplate: (templateId: string) => void;
    onStartWithoutTemplate: () => void;
}

export default function PromptTemplateSelector({
    onSelectTemplate,
    onStartWithoutTemplate
}: PromptTemplateSelectorProps) {
    const [templates, setTemplates] = useState<PromptTemplate[]>([]);
    const [favorites, setFavorites] = useState<PromptTemplate[]>([]);
    const [recent, setRecent] = useState<PromptTemplate[]>([]);
    const [loading, setLoading] = useState(true);
    const [activeTab, setActiveTab] = useState<'all' | 'favorites' | 'recent'>('favorites');

    useEffect(() => {
        loadTemplates();
    }, []);

    const loadTemplates = async () => {
        try {
            const [allResp, favResp, recResp] = await Promise.all([
                fetch('/api/prompttemplate'),
                fetch('/api/prompttemplate/favorites'),
                fetch('/api/prompttemplate/recent?limit=5'),
            ]);

            const allData = await allResp.json();
            const favData = await favResp.json();
            const recData = await recResp.json();

            setTemplates(allData);
            setFavorites(favData);
            setRecent(recData);
        } catch (error) {
            console.error('Failed to load prompt templates:', error);
        } finally {
            setLoading(false);
        }
    };

    const currentTemplates =
        activeTab === 'favorites' ? favorites :
        activeTab === 'recent' ? recent :
        templates;

    const getIconEmoji = (icon?: string) => {
        const iconMap: Record<string, string> = {
            'bug': '🐛',
            'test-tube': '🧪',
            'clipboard-check': '📋',
            'code': '💻',
            'rocket': '🚀',
            'gear': '⚙️',
        };
        return iconMap[icon || ''] || '📝';
    };

    return (
        <div className="prompt-template-selector">
            <div className="header">
                <h2>Start Claude Code Session</h2>
                <p>Choose a predefined prompt template or start a blank session</p>
            </div>

            <div className="tabs">
                <button
                    className={activeTab === 'favorites' ? 'active' : ''}
                    onClick={() => setActiveTab('favorites')}
                >
                    ⭐ Favorites ({favorites.length})
                </button>
                <button
                    className={activeTab === 'recent' ? 'active' : ''}
                    onClick={() => setActiveTab('recent')}
                >
                    🕒 Recent ({recent.length})
                </button>
                <button
                    className={activeTab === 'all' ? 'active' : ''}
                    onClick={() => setActiveTab('all')}
                >
                    📚 All Templates ({templates.length})
                </button>
            </div>

            {loading ? (
                <div className="loading">Loading templates...</div>
            ) : (
                <div className="template-list">
                    {currentTemplates.length === 0 ? (
                        <div className="no-templates">
                            <p>No templates found</p>
                            <button
                                className="btn-primary"
                                onClick={onStartWithoutTemplate}
                            >
                                Start Blank Session
                            </button>
                        </div>
                    ) : (
                        <>
                            {currentTemplates.map(template => (
                                <div
                                    key={template.id}
                                    className="template-card"
                                    onClick={() => onSelectTemplate(template.id)}
                                >
                                    <div className="template-icon">
                                        {getIconEmoji(template.icon)}
                                    </div>
                                    <div className="template-content">
                                        <div className="template-header">
                                            <h3>{template.name}</h3>
                                            {template.isFavorite && <span className="favorite-badge">⭐</span>}
                                        </div>
                                        {template.description && (
                                            <p className="template-description">{template.description}</p>
                                        )}
                                        <div className="template-meta">
                                            {template.category && (
                                                <span className="category-badge">{template.category}</span>
                                            )}
                                            <span className="usage-count">
                                                Used {template.usageCount} {template.usageCount === 1 ? 'time' : 'times'}
                                            </span>
                                            {template.autoExecute && (
                                                <span className="auto-execute-badge">⚡ Auto-execute</span>
                                            )}
                                        </div>
                                        <div className="template-prompt-preview">
                                            <code>{template.promptText.substring(0, 100)}...</code>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </>
                    )}
                </div>
            )}

            <div className="actions">
                <button
                    className="btn-secondary"
                    onClick={onStartWithoutTemplate}
                >
                    Start Blank Session (No Template)
                </button>
            </div>

            <style>{`
                .prompt-template-selector {
                    padding: 20px;
                    max-width: 800px;
                    margin: 0 auto;
                }

                .header {
                    text-align: center;
                    margin-bottom: 30px;
                }

                .header h2 {
                    margin: 0 0 10px 0;
                    font-size: 24px;
                    font-weight: 600;
                }

                .header p {
                    margin: 0;
                    color: #666;
                    font-size: 14px;
                }

                .tabs {
                    display: flex;
                    gap: 10px;
                    margin-bottom: 20px;
                    border-bottom: 2px solid #eee;
                }

                .tabs button {
                    padding: 10px 20px;
                    background: none;
                    border: none;
                    border-bottom: 2px solid transparent;
                    cursor: pointer;
                    font-size: 14px;
                    color: #666;
                    transition: all 0.2s;
                }

                .tabs button:hover {
                    color: #333;
                }

                .tabs button.active {
                    color: #0066cc;
                    border-bottom-color: #0066cc;
                }

                .template-list {
                    display: flex;
                    flex-direction: column;
                    gap: 15px;
                    margin-bottom: 20px;
                }

                .template-card {
                    display: flex;
                    gap: 15px;
                    padding: 15px;
                    border: 1px solid #ddd;
                    border-radius: 8px;
                    cursor: pointer;
                    transition: all 0.2s;
                    background: white;
                }

                .template-card:hover {
                    border-color: #0066cc;
                    box-shadow: 0 2px 8px rgba(0, 102, 204, 0.1);
                }

                .template-icon {
                    font-size: 32px;
                    line-height: 1;
                }

                .template-content {
                    flex: 1;
                }

                .template-header {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    margin-bottom: 8px;
                }

                .template-header h3 {
                    margin: 0;
                    font-size: 16px;
                    font-weight: 600;
                }

                .favorite-badge {
                    font-size: 14px;
                }

                .template-description {
                    margin: 0 0 10px 0;
                    font-size: 14px;
                    color: #666;
                }

                .template-meta {
                    display: flex;
                    gap: 10px;
                    align-items: center;
                    margin-bottom: 10px;
                    flex-wrap: wrap;
                }

                .category-badge,
                .usage-count,
                .auto-execute-badge {
                    font-size: 12px;
                    padding: 3px 8px;
                    border-radius: 4px;
                }

                .category-badge {
                    background: #e3f2fd;
                    color: #1976d2;
                }

                .usage-count {
                    background: #f5f5f5;
                    color: #666;
                }

                .auto-execute-badge {
                    background: #fff3e0;
                    color: #f57c00;
                }

                .template-prompt-preview {
                    background: #f9f9f9;
                    padding: 8px;
                    border-radius: 4px;
                }

                .template-prompt-preview code {
                    font-family: 'Courier New', monospace;
                    font-size: 12px;
                    color: #333;
                }

                .no-templates {
                    text-align: center;
                    padding: 40px;
                    color: #666;
                }

                .actions {
                    text-align: center;
                }

                .btn-primary,
                .btn-secondary {
                    padding: 12px 24px;
                    border: none;
                    border-radius: 6px;
                    font-size: 14px;
                    font-weight: 500;
                    cursor: pointer;
                    transition: all 0.2s;
                }

                .btn-primary {
                    background: #0066cc;
                    color: white;
                }

                .btn-primary:hover {
                    background: #0052a3;
                }

                .btn-secondary {
                    background: #f5f5f5;
                    color: #333;
                }

                .btn-secondary:hover {
                    background: #e0e0e0;
                }

                .loading {
                    text-align: center;
                    padding: 40px;
                    color: #666;
                }
            `}</style>
        </div>
    );
}
