
import React, { useState } from 'react';
import { Cpu, Loader2, Lock, Mail, User, Sun, Moon } from 'lucide-react';
import { login, register } from '../services/authService';
import { validatePassword } from '../utils/passwordValidator';

interface LoginProps {
  onLogin: () => void;
  isDarkMode: boolean;
  toggleTheme: () => void;
}

const Login: React.FC<LoginProps> = ({ onLogin, isDarkMode, toggleTheme }) => {
  const [isRegistering, setIsRegistering] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    
    if (!email || !password) {
      setError('Please fill in all required fields.');
      return;
    }

    if (isRegistering && !fullName) {
      setError('Please enter your full name.');
      return;
    }

    // Validate password for registration
    if (isRegistering) {
      const validation = validatePassword(password);
      if (!validation.isValid) {
        setError(validation.errors[0]); // Show first error
        return;
      }
    }

    setIsLoading(true);

    try {
      if (isRegistering) {
        await register(email, password, fullName);
      } else {
        await login(email, password);
      }
      onLogin();
    } catch (err: any) {
      setError(err.message || 'Authentication failed. Please check your credentials.');
    } finally {
      setIsLoading(false);
    }
  };

  const toggleMode = () => {
    setIsRegistering(!isRegistering);
    setError('');
  };

  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-4 relative overflow-hidden transition-colors duration-300">
      {/* Theme Toggle */}
      <div className="absolute top-6 right-6 z-20 animate-fade-in">
        <button 
          onClick={toggleTheme} 
          className="p-2 rounded-full text-textMuted hover:text-text bg-surface/50 hover:bg-surface border border-transparent hover:border-border backdrop-blur-sm transition-all shadow-sm"
          title={isDarkMode ? "Switch to Light Mode" : "Switch to Dark Mode"}
        >
          {isDarkMode ? <Sun className="w-5 h-5" /> : <Moon className="w-5 h-5" />}
        </button>
      </div>

      {/* Background decoration */}
      <div className="absolute top-0 left-0 w-full h-full overflow-hidden pointer-events-none z-0">
        <div className="absolute top-[-10%] left-[-10%] w-[40%] h-[40%] bg-primary/10 rounded-full blur-[100px]" />
        <div className="absolute bottom-[-10%] right-[-10%] w-[40%] h-[40%] bg-purple-500/10 rounded-full blur-[100px]" />
      </div>

      <div className="w-full max-w-md bg-surface border border-border rounded-xl shadow-2xl relative z-10 animate-fade-in">
        <div className="p-8 pb-6 text-center">
          <div className="w-16 h-16 bg-primary rounded-2xl flex items-center justify-center mx-auto mb-6 shadow-lg shadow-primary/20">
            <Cpu className="w-8 h-8 text-white" />
          </div>
          <h1 className="text-2xl font-bold text-text mb-2">
            {isRegistering ? 'Create Account' : 'Welcome Back'}
          </h1>
          <p className="text-textMuted text-sm">
            {isRegistering ? 'Join Orchestra to start automating.' : 'Enter your credentials to access Orchestra.'}
          </p>
        </div>

        <form onSubmit={handleSubmit} className="p-8 pt-0 space-y-5">
          {error && (
            <div className="p-3 bg-red-500/10 border border-red-500/20 rounded text-red-400 text-xs font-medium">
              {error}
            </div>
          )}

          {isRegistering && (
            <div className="space-y-1.5 animate-fade-in">
              <label className="text-xs font-semibold text-textMuted uppercase block">Full Name</label>
              <div className="relative">
                <User className="absolute left-3 top-2.5 w-4 h-4 text-textMuted" />
                <input 
                  type="text" 
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  className="w-full bg-background border border-border rounded-md pl-10 pr-3 py-2 text-sm text-text focus:outline-none focus:border-primary transition-colors placeholder:text-textMuted/50"
                  placeholder="John Doe"
                />
              </div>
            </div>
          )}

          <div className="space-y-1.5">
            <label className="text-xs font-semibold text-textMuted uppercase block">Email Address</label>
            <div className="relative">
              <Mail className="absolute left-3 top-2.5 w-4 h-4 text-textMuted" />
              <input 
                type="email" 
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full bg-background border border-border rounded-md pl-10 pr-3 py-2 text-sm text-text focus:outline-none focus:border-primary transition-colors placeholder:text-textMuted/50"
                placeholder="name@company.com"
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <label className="text-xs font-semibold text-textMuted uppercase block">Password</label>
            <div className="relative">
              <Lock className="absolute left-3 top-2.5 w-4 h-4 text-textMuted" />
              <input 
                type="password" 
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full bg-background border border-border rounded-md pl-10 pr-3 py-2 text-sm text-text focus:outline-none focus:border-primary transition-colors placeholder:text-textMuted/50"
                placeholder={isRegistering ? "Min 8 chars, 1 uppercase, 1 digit, 1 special" : "••••••••"}
              />
            </div>
          </div>

          <button 
            type="submit" 
            disabled={isLoading}
            className="w-full bg-primary hover:bg-primaryHover text-white py-2.5 rounded-md text-sm font-medium transition-colors flex items-center justify-center gap-2 mt-2 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : (isRegistering ? 'Create Account' : 'Sign In')}
          </button>
        </form>

        <div className="p-4 border-t border-border bg-surfaceHighlight/30 text-center">
          <p className="text-xs text-textMuted">
            {isRegistering ? 'Already have an account? ' : "Don't have an account? "}
            <button onClick={toggleMode} className="text-primary cursor-pointer hover:underline font-medium">
              {isRegistering ? 'Sign In' : 'Sign Up'}
            </button>
          </p>
        </div>
      </div>
    </div>
  );
};

export default Login;
