'use client';

import { useEffect, useState } from 'react';
import { GoogleLogin } from '@react-oauth/google';
import { authService, trackingService, mediaService } from '@/lib/api/services';
import type { TrackingEvent, MediaType } from '@/lib/types';

export default function Home() {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [authMode, setAuthMode] = useState<'login' | 'register'>('login');
  const [trackingEvents, setTrackingEvents] = useState<TrackingEvent[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Login form
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  // Register form
  const [registerEmail, setRegisterEmail] = useState('');
  const [registerUsername, setRegisterUsername] = useState('');
  const [registerPassword, setRegisterPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  // Add media form
  const [mediaTitle, setMediaTitle] = useState('');
  const [mediaType, setMediaType] = useState<MediaType>('Film');

  useEffect(() => {
    setIsAuthenticated(authService.isAuthenticated());
  }, []);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const response = await authService.login(email, password);
      if (response.success) {
        setIsAuthenticated(true);
        setEmail('');
        setPassword('');
        alert(response.message);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al iniciar sesión');
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    if (registerPassword !== confirmPassword) {
      setError('Las contraseñas no coinciden');
      setLoading(false);
      return;
    }

    try {
      const response = await authService.register(registerEmail, registerUsername, registerPassword);
      if (response.success) {
        alert(response.message);
        setAuthMode('login');
        setRegisterEmail('');
        setRegisterUsername('');
        setRegisterPassword('');
        setConfirmPassword('');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al registrarse');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    authService.logout();
    setIsAuthenticated(false);
    setTrackingEvents([]);
  };

  const loadTrackingEvents = async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await trackingService.getTrackingEvents();
      setTrackingEvents(response.data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al cargar eventos');
    } finally {
      setLoading(false);
    }
  };

  const handleAddMedia = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const response = await mediaService.addMedia(mediaTitle, mediaType);
      if (response.success) {
        alert(response.message);
        setMediaTitle('');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al agregar media');
    } finally {
      setLoading(false);
    }
  };

  if (!isAuthenticated) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-zinc-50 dark:bg-black">
        <div className="w-full max-w-md p-8 bg-white dark:bg-zinc-900 rounded-lg shadow-lg">
          <h1 className="text-2xl font-bold mb-6 text-center text-zinc-900 dark:text-white">
            {authMode === 'login' ? 'MediaTracker Login' : 'MediaTracker Register'}
          </h1>

          {authMode === 'login' ? (
            <form onSubmit={handleLogin} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">
                  Email
                </label>
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full px-4 py-2 border border-zinc-300 dark:border-zinc-700 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-zinc-800 dark:text-white"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">
                  Password
                </label>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full px-4 py-2 border border-zinc-300 dark:border-zinc-700 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-zinc-800 dark:text-white"
                  required
                />
              </div>
              {error && (
                <div className="p-3 bg-red-100 dark:bg-red-900 text-red-700 dark:text-red-200 rounded-lg text-sm">
                  {error}
                </div>
              )}
              <button
                type="submit"
                disabled={loading}
                className="w-full py-2 px-4 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {loading ? 'Cargando...' : 'Iniciar Sesión'}
              </button>
              <div className="relative my-2">
                <div className="absolute inset-0 flex items-center">
                  <div className="w-full border-t border-zinc-300 dark:border-zinc-700" />
                </div>
                <div className="relative flex justify-center text-xs text-zinc-500 dark:text-zinc-400">
                  <span className="bg-white dark:bg-zinc-900 px-2">o continúa con</span>
                </div>
              </div>
              <div className="flex justify-center">
                <GoogleLogin
                  onSuccess={(res) => {
                    if (res.credential) {
                      setLoading(true);
                      setError(null);
                      authService.googleLogin(res.credential)
                        .then(() => setIsAuthenticated(true))
                        .catch((err) => setError(err instanceof Error ? err.message : 'Error con Google login'))
                        .finally(() => setLoading(false));
                    }
                  }}
                  onError={() => setError('Error al iniciar sesión con Google')}
                />
              </div>
              <div className="text-center">
                <p className="text-sm text-zinc-600 dark:text-zinc-400">
                  ¿No tienes cuenta?{' '}
                  <button
                    type="button"
                    onClick={() => setAuthMode('register')}
                    className="text-blue-600 hover:text-blue-700 font-medium"
                  >
                    Regístrate
                  </button>
                </p>
              </div>
            </form>
          ) : (
            <form onSubmit={handleRegister} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">
                  Email
                </label>
                <input
                  type="email"
                  value={registerEmail}
                  onChange={(e) => setRegisterEmail(e.target.value)}
                  className="w-full px-4 py-2 border border-zinc-300 dark:border-zinc-700 rounded-lg focus:ring-2 focus:ring-green-500 dark:bg-zinc-800 dark:text-white"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">
                  Username
                </label>
                <input
                  type="text"
                  value={registerUsername}
                  onChange={(e) => setRegisterUsername(e.target.value)}
                  className="w-full px-4 py-2 border border-zinc-300 dark:border-zinc-700 rounded-lg focus:ring-2 focus:ring-green-500 dark:bg-zinc-800 dark:text-white"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">
                  Password
                </label>
                <input
                  type="password"
                  value={registerPassword}
                  onChange={(e) => setRegisterPassword(e.target.value)}
                  className="w-full px-4 py-2 border border-zinc-300 dark:border-zinc-700 rounded-lg focus:ring-2 focus:ring-green-500 dark:bg-zinc-800 dark:text-white"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">
                  Confirmar Password
                </label>
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  className="w-full px-4 py-2 border border-zinc-300 dark:border-zinc-700 rounded-lg focus:ring-2 focus:ring-green-500 dark:bg-zinc-800 dark:text-white"
                  required
                />
              </div>
              {error && (
                <div className="p-3 bg-red-100 dark:bg-red-900 text-red-700 dark:text-red-200 rounded-lg text-sm">
                  {error}
                </div>
              )}
              <button
                type="submit"
                disabled={loading}
                className="w-full py-2 px-4 bg-green-600 hover:bg-green-700 text-white font-medium rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {loading ? 'Registrando...' : 'Registrarse'}
              </button>
              <div className="text-center">
                <p className="text-sm text-zinc-600 dark:text-zinc-400">
                  ¿Ya tienes cuenta?{' '}
                  <button
                    type="button"
                    onClick={() => setAuthMode('login')}
                    className="text-blue-600 hover:text-blue-700 font-medium"
                  >
                    Inicia sesión
                  </button>
                </p>
              </div>
            </form>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-black p-8">
      <div className="max-w-6xl mx-auto">
        <div className="flex justify-between items-center mb-8">
          <h1 className="text-3xl font-bold text-zinc-900 dark:text-white">
            MediaTracker Dashboard
          </h1>
          <button
            onClick={handleLogout}
            className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg"
          >
            Cerrar Sesión
          </button>
        </div>

        {error && (
          <div className="mb-4 p-4 bg-red-100 dark:bg-red-900 text-red-700 dark:text-red-200 rounded-lg">
            {error}
          </div>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
          <div className="bg-white dark:bg-zinc-900 p-6 rounded-lg shadow">
            <h2 className="text-xl font-semibold mb-4 text-zinc-900 dark:text-white">
              Agregar Media
            </h2>
            <form onSubmit={handleAddMedia} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">
                  Título
                </label>
                <input
                  type="text"
                  value={mediaTitle}
                  onChange={(e) => setMediaTitle(e.target.value)}
                  className="w-full px-4 py-2 border border-zinc-300 dark:border-zinc-700 rounded-lg dark:bg-zinc-800 dark:text-white"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-zinc-700 dark:text-zinc-300 mb-2">
                  Tipo
                </label>
                <select
                  value={mediaType}
                  onChange={(e) => setMediaType(e.target.value as MediaType)}
                  className="w-full px-4 py-2 border border-zinc-300 dark:border-zinc-700 rounded-lg dark:bg-zinc-800 dark:text-white"
                >
                  <option value="Film">Film</option>
                  <option value="Serie">Serie</option>
                  <option value="Book">Book</option>
                  <option value="Comic">Comic</option>
                </select>
              </div>
              <button
                type="submit"
                disabled={loading}
                className="w-full py-2 px-4 bg-blue-600 hover:bg-blue-700 text-white rounded-lg disabled:opacity-50"
              >
                {loading ? 'Agregando...' : 'Agregar Media'}
              </button>
            </form>
          </div>

          <div className="bg-white dark:bg-zinc-900 p-6 rounded-lg shadow">
            <h2 className="text-xl font-semibold mb-4 text-zinc-900 dark:text-white">
              Tracking Events
            </h2>
            <button
              onClick={loadTrackingEvents}
              disabled={loading}
              className="w-full py-2 px-4 bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50"
            >
              {loading ? 'Cargando...' : 'Cargar Eventos'}
            </button>
            <p className="mt-4 text-sm text-zinc-600 dark:text-zinc-400">
              Total eventos: {trackingEvents.length}
            </p>
          </div>
        </div>

        {trackingEvents.length > 0 && (
          <div className="bg-white dark:bg-zinc-900 p-6 rounded-lg shadow">
            <h2 className="text-xl font-semibold mb-4 text-zinc-900 dark:text-white">
              Mis Eventos de Tracking
            </h2>
            <div className="space-y-3">
              {trackingEvents.map((event) => (
                <div
                  key={event.id}
                  className="p-4 border border-zinc-200 dark:border-zinc-700 rounded-lg"
                >
                  <div className="flex justify-between items-start">
                    <div>
                      <h3 className="font-semibold text-zinc-900 dark:text-white">
                        {event.media?.title || `Media ID: ${event.mediaId}`}
                      </h3>
                      <p className="text-sm text-zinc-600 dark:text-zinc-400">
                        Tipo: {event.media?.type} | Progreso: {event.progress}%
                      </p>
                      <p className="text-xs text-zinc-500 dark:text-zinc-500">
                        {new Date(event.eventDate).toLocaleString()}
                      </p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

