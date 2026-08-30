# Circle of Fifths

An interactive web application to explore and visualize the Circle of Fifths, built with React (Vite, TypeScript, Tailwind CSS) for the frontend and Node.js (Express, TypeScript) for the backend.

## Project Structure

- `frontend/` — React frontend using Vite, TypeScript, Tailwind CSS
- `backend/` — Express backend with TypeScript
- `shared/` — Shared music theory logic (note names, intervals, etc.) in TypeScript

## Getting Started

### Prerequisites
- Node.js (v18+ recommended)
- npm or yarn

### Setup

#### 1. Install dependencies

```
cd frontend
npm install
cd ../backend
npm install
```

#### 2. Run the development servers

- **Frontend:**
    ```
    cd frontend
    npm run dev
    ```
- **Backend:**
    ```
    cd backend
    npm run dev
    ```

#### 3. Develop shared logic

- Place shared TypeScript modules in the `shared/` directory. Both frontend and backend can import from here using appropriate build tooling (see documentation for monorepos or module aliases).

---

## License
MIT
