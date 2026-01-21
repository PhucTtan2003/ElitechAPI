// src/entries/user.tsx
import ReactDOM from 'react-dom/client'
import App from '../App'

const el = document.getElementById('react-root-user')
if (el) ReactDOM.createRoot(el).render(<App who="User" />)
