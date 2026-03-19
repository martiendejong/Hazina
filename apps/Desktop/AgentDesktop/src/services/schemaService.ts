import yaml from 'js-yaml';
import { UIComponentDefinition } from '../types/schema';

class SchemaService {
  private schemas: Map<string, UIComponentDefinition> = new Map();
  private schemasPath: string = '/schemas'; // Vite public folder path

  async loadSchemas(): Promise<void> {
    const componentIds = [
      'ui.task-card',
      'ui.file-tree',
      'ui.approval-panel',
      'ui.progress-tracker',
      'ui.command-preview',
    ];

    await Promise.all(
      componentIds.map(async (id) => {
        try {
          const response = await fetch(`${this.schemasPath}/${id}.component.yaml`);
          if (!response.ok) {
            console.warn(`Failed to load schema for ${id}`);
            return;
          }
          const yamlText = await response.text();
          const schema = yaml.load(yamlText) as UIComponentDefinition;
          this.schemas.set(id, schema);
        } catch (error) {
          console.error(`Error loading schema for ${id}:`, error);
        }
      })
    );

    console.log(`Loaded ${this.schemas.size} component schemas`);
  }

  getSchema(componentId: string): UIComponentDefinition | undefined {
    return this.schemas.get(componentId);
  }

  getAllSchemas(): UIComponentDefinition[] {
    return Array.from(this.schemas.values());
  }

  validateData(componentId: string, data: any): { valid: boolean; errors: string[] } {
    const schema = this.schemas.get(componentId);
    if (!schema) {
      return { valid: false, errors: [`Unknown component: ${componentId}`] };
    }

    const errors: string[] = [];

    // Check required fields
    if (schema.schema.required) {
      for (const field of schema.schema.required) {
        if (!(field in data)) {
          errors.push(`Missing required field: ${field}`);
        }
      }
    }

    // Validate properties (basic validation)
    for (const [key, propSchema] of Object.entries(schema.schema.properties)) {
      if (key in data) {
        const value = data[key];
        const propType = (propSchema as any).type;

        // Type checking
        if (propType === 'string' && typeof value !== 'string') {
          errors.push(`Field ${key} must be a string`);
        } else if (propType === 'number' && typeof value !== 'number') {
          errors.push(`Field ${key} must be a number`);
        } else if (propType === 'boolean' && typeof value !== 'boolean') {
          errors.push(`Field ${key} must be a boolean`);
        } else if (propType === 'array' && !Array.isArray(value)) {
          errors.push(`Field ${key} must be an array`);
        } else if (propType === 'object' && typeof value !== 'object') {
          errors.push(`Field ${key} must be an object`);
        }

        // Max length
        if ((propSchema as any).maxLength && typeof value === 'string') {
          if (value.length > (propSchema as any).maxLength) {
            errors.push(`Field ${key} exceeds max length of ${(propSchema as any).maxLength}`);
          }
        }

        // Enum validation
        if ((propSchema as any).enum && !(propSchema as any).enum.includes(value)) {
          errors.push(`Field ${key} must be one of: ${(propSchema as any).enum.join(', ')}`);
        }
      }
    }

    return { valid: errors.length === 0, errors };
  }
}

export const schemaService = new SchemaService();
export default schemaService;
