import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListResourcesRequestSchema,
  ListToolsRequestSchema,
  ReadResourceRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';

interface Topic {
  id: string;
  title: string;
  content: string;
  parentId: string | null;
  tags: string[];
}

interface Store {
  topics: Record<string, Topic>;
}

const storePath = resolve(process.cwd(), 'knowledge.json');
let store: Store = { topics: {} };

function load() {
  if (existsSync(storePath)) {
    store = JSON.parse(readFileSync(storePath, 'utf8')) as Store;
  } else {
    store = {
      topics: {
        root: {
          id: 'root',
          title: 'Circle App',
          content: 'Root knowledge topic for the Circle application.',
          parentId: null,
          tags: [],
        },
      },
    };
    save();
  }
}

function save() {
  writeFileSync(storePath, JSON.stringify(store, null, 2));
}

function getOrThrow(id: string): Topic {
  const t = store.topics[id];
  if (!t) throw new Error(`Topic not found: ${id}`);
  return t;
}

function childrenOf(parentId: string | null): string[] {
  return Object.values(store.topics)
    .filter((t) => t.parentId === parentId)
    .sort((a, b) => a.title.localeCompare(b.title))
    .map((t) => t.id);
}

function isDescendant(ancestorId: string, childId: string): boolean {
  const t = store.topics[childId];
  if (!t) return false;
  if (t.parentId === ancestorId) return true;
  if (t.parentId === null) return false;
  return isDescendant(ancestorId, t.parentId);
}

function toText(value: unknown): { type: 'text'; text: string } {
  return { type: 'text', text: JSON.stringify(value, null, 2) };
}

const tools = [
  {
    name: 'list_topics',
    description: 'List topics under a parent. Omit parentId to list topics under root.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        parentId: {
          type: 'string' as const,
          description: 'Parent topic id',
        },
      },
    },
  },
  {
    name: 'get_topic',
    description: 'Get a topic by id, including its direct child ids.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        id: {
          type: 'string' as const,
          description: 'Topic id',
        },
      },
      required: ['id'],
    },
  },
  {
    name: 'search_topics',
    description: 'Search topics by id, title, content or tags.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        query: {
          type: 'string' as const,
          description: 'Search query',
        },
      },
      required: ['query'],
    },
  },
  {
    name: 'add_topic',
    description: 'Add a new topic.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        id: { type: 'string' as const, description: 'Unique topic id' },
        title: { type: 'string' as const, description: 'Topic title' },
        content: { type: 'string' as const, description: 'Topic content' },
        parentId: {
          anyOf: [{ type: 'string' as const }, { type: 'null' as const }],
          description: 'Parent topic id; defaults to root',
        },
        tags: {
          type: 'array' as const,
          items: { type: 'string' as const },
          description: 'Tags',
        },
      },
      required: ['id', 'title'],
    },
  },
  {
    name: 'update_topic',
    description: 'Update an existing topic.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        id: { type: 'string' as const, description: 'Topic id' },
        title: { type: 'string' as const, description: 'New title' },
        content: { type: 'string' as const, description: 'New content' },
        parentId: {
          anyOf: [{ type: 'string' as const }, { type: 'null' as const }],
          description: 'New parent topic id',
        },
        tags: {
          type: 'array' as const,
          items: { type: 'string' as const },
          description: 'New tags',
        },
      },
      required: ['id'],
    },
  },
  {
    name: 'delete_topic',
    description: 'Delete a topic. Cannot delete root or topics with children.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        id: { type: 'string' as const, description: 'Topic id' },
      },
      required: ['id'],
    },
  },
];

load();

const server = new Server(
  { name: 'circle-knowledge', version: '1.0.0' },
  { capabilities: { tools: {}, resources: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools }));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name } = request.params;
  const args = (request.params.arguments ?? {}) as Record<string, unknown>;
  try {
    switch (name) {
      case 'list_topics': {
        const parentId = typeof args.parentId === 'string' ? args.parentId : 'root';
        const ids = childrenOf(parentId);
        return { content: ids.map((id) => toText(store.topics[id])) };
      }
      case 'get_topic': {
        const id = String(args.id);
        const t = getOrThrow(id);
        return { content: [toText({ ...t, children: childrenOf(id) })] };
      }
      case 'search_topics': {
        const q = String(args.query).toLowerCase();
        const matches = Object.values(store.topics).filter(
          (t) =>
            t.id.toLowerCase().includes(q) ||
            t.title.toLowerCase().includes(q) ||
            t.content.toLowerCase().includes(q) ||
            t.tags.some((tag) => tag.toLowerCase().includes(q))
        );
        return { content: [toText(matches)] };
      }
      case 'add_topic': {
        const id = String(args.id);
        if (store.topics[id]) throw new Error(`Topic already exists: ${id}`);
        const title = String(args.title);
        const content = typeof args.content === 'string' ? args.content : '';
        const parentId =
          args.parentId === null ? null : typeof args.parentId === 'string' ? args.parentId : 'root';
        if (parentId !== null) getOrThrow(parentId);
        const tags = Array.isArray(args.tags)
          ? args.tags.filter((x): x is string => typeof x === 'string')
          : [];
        store.topics[id] = { id, title, content, parentId, tags };
        save();
        return { content: [toText(store.topics[id])] };
      }
      case 'update_topic': {
        const id = String(args.id);
        const t = getOrThrow(id);
        if (typeof args.title === 'string') t.title = args.title;
        if (typeof args.content === 'string') t.content = args.content;
        if (args.tags !== undefined) {
          t.tags = Array.isArray(args.tags)
            ? args.tags.filter((x): x is string => typeof x === 'string')
            : [];
        }
        if (args.parentId !== undefined) {
          const newParent = args.parentId === null ? null : String(args.parentId);
          if (newParent === id) throw new Error('Cannot move topic into itself');
          if (newParent !== null) {
            getOrThrow(newParent);
            if (isDescendant(id, newParent)) throw new Error('Cannot move topic into its own descendant');
          }
          t.parentId = newParent;
        }
        save();
        return { content: [toText(t)] };
      }
      case 'delete_topic': {
        const id = String(args.id);
        if (id === 'root') throw new Error('Cannot delete root topic');
        getOrThrow(id);
        const hasChildren = Object.values(store.topics).some((t) => t.parentId === id);
        if (hasChildren) throw new Error('Cannot delete topic with children');
        delete store.topics[id];
        save();
        return { content: [{ type: 'text', text: `Deleted topic: ${id}` }] };
      }
      default:
        throw new Error(`Unknown tool: ${name}`);
    }
  } catch (err: any) {
    return { content: [{ type: 'text', text: `Error: ${err.message}` }], isError: true };
  }
});

server.setRequestHandler(ListResourcesRequestSchema, async () => ({
  resources: Object.values(store.topics).map((t) => ({
    uri: `topic://${t.id}`,
    name: t.title,
    mimeType: 'application/json',
  })),
}));

server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
  const uri = request.params.uri;
  const match = /^topic:\/\/(.+)$/.exec(uri);
  if (!match) throw new Error(`Invalid resource uri: ${uri}`);
  const id = match[1];
  const t = getOrThrow(id);
  return {
    contents: [
      {
        uri,
        mimeType: 'application/json',
        text: JSON.stringify({ ...t, children: childrenOf(id) }, null, 2),
      },
    ],
  };
});

const transport = new StdioServerTransport();
await server.connect(transport);
