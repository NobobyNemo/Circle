---
name: circle-kb
description: Работа с базой знаний Circle через MCP-сервер circle-knowledge
triggers:
  - user
  - model
allowed-tools:
  - read
  - edit
  - write
  - mcp__circle-knowledge__list_topics
  - mcp__circle-knowledge__get_topic
  - mcp__circle-knowledge__search_topics
  - mcp__circle-knowledge__add_topic
  - mcp__circle-knowledge__update_topic
  - mcp__circle-knowledge__delete_topic
permissions:
  allow:
    - Read(csharp/**)
    - Read(mcp-knowledge/**)
    - Read(.devin/**)
    - Write(knowledge.json)
    - Write(.devin/**)
    - Write(mcp-knowledge/**)
---

Работай с базой знаний `circle-knowledge` как с первоисточником информации о проекте.

1. Перед ответом на вопросы по архитектуре, домену, музыкальным алгоритмам, аудио, UI, ViewModels, сборке и т.п. сначала вызывай `mcp__circle-knowledge__search_topics` или `mcp__circle-knowledge__get_topic`.
2. Если в базе не хватает информации — сначала ищи нужную в коде (read/grep), затем добавляй/обновляй топик через `mcp__circle-knowledge__add_topic` / `update_topic`.
3. Если ты ищешь информацию в интернете, документации или других источниках и находишь что-то полезное и переиспользуемое для проекта, обязательно заноси это в базу знаний под подходящим родителем.
4. Держи топики краткими, но исчерпывающими. Используй теги для поиска. Не дублируй информацию.
5. После любых значимых изменений в коде (новые компоненты, изменение API, логики, аудио, UI) обновляй соответствующий топик в базе знаний.

Пример вызовов:
- `mcp__circle-knowledge__search_topics` с аргументом `{ "query": "tuner" }`
- `mcp__circle-knowledge__add_topic` с аргументами `{ "id": "new-topic", "title": "...", "content": "...", "parentId": "audio", "tags": ["..."] }`
