import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import { isWindowsLoopbackDashboard } from './hooks/useHostBridge';

if (isWindowsLoopbackDashboard()) {
  document.documentElement.classList.add('windows-loopback-dashboard');
} else {
  document.documentElement.classList.remove('windows-loopback-dashboard');
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
