import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';

import './i18n/i18n';
import './styles/global.css';
import App from './App';
import { bootstrapSession } from './api/httpClient';

// Try to restore a session from the HttpOnly refresh cookie before the first
// paint decides what to render. It resolves either way — "not signed in" is the
// normal outcome, not a failure — so rendering is never blocked on it.
void bootstrapSession();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>
);
