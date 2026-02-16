
import { User } from '../types';

interface AuthResponse {
  token: string;
  user: User;
}

// API base URL from Aspire service discovery (injected via Vite)
const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/auth`;

export const login = async (email: string, password: string): Promise<AuthResponse> => {
  try {
    const response = await fetch(`${API_BASE_URL}/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) {
        throw new Error("API endpoint not found (returned HTML)");
    }

    if (!response.ok) {
        if (response.status === 404) throw new Error("API endpoint not found");
        if (response.status === 401) {
            try {
                const errorData = await response.json();
                throw new Error(errorData.detail || errorData.message || 'Invalid credentials');
            } catch {
                throw new Error('Invalid credentials');
            }
        }
        throw new Error('Login failed');
    }

    const data = await response.json();
    localStorage.setItem('nexus_token', data.token);
    localStorage.setItem('nexus_user', JSON.stringify(data.user));
    return data;
  } catch (error) {
    throw error;
  }
};

export const register = async (email: string, password: string, fullName: string): Promise<AuthResponse> => {
  try {
    const response = await fetch(`${API_BASE_URL}/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, name: fullName }),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) {
        throw new Error("API endpoint not found (returned HTML)");
    }

    if (!response.ok) {
        if (response.status === 404) throw new Error("API endpoint not found");
        if (response.status === 400 || response.status === 409) {
            try {
                const errorData = await response.json();
                // Validation errors come in 'detail' field, duplicates in 'message'
                throw new Error(errorData.detail || errorData.message || 'Registration failed: Invalid data');
            } catch (e: any) {
                if (e.message && !e.message.includes('endpoint')) {
                    throw e;
                }
                throw new Error('Registration failed: Invalid data');
            }
        }
        throw new Error('Registration failed');
    }

    const data = await response.json();
    localStorage.setItem('nexus_token', data.token);
    localStorage.setItem('nexus_user', JSON.stringify(data.user));
    return data;
  } catch (error) {
    throw error;
  }
};

export const updateUser = async (data: { name: string, email: string }): Promise<User> => {
  try {
    const response = await fetch(`${API_BASE_URL}/profile`, {
      method: 'PATCH',
      headers: { 
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('nexus_token')}`
      },
      body: JSON.stringify(data),
    });

    if (!response.ok) throw new Error("Update failed");
    const updatedUser = await response.json();
    localStorage.setItem('nexus_user', JSON.stringify(updatedUser));
    return updatedUser;
  } catch (e) {
    throw e;
  }
};

export const changePassword = async (currentPassword: string, newPassword: string): Promise<void> => {
    try {
        const response = await fetch(`${API_BASE_URL}/change-password`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${localStorage.getItem('nexus_token')}`
            },
            body: JSON.stringify({ currentPassword, newPassword }),
        });

        if (!response.ok) {
            try {
                const err = await response.json();
                // Validation errors come in 'detail' field
                throw new Error(err.detail || err.message || "Failed to change password");
            } catch (e: any) {
                if (e.message && !e.message.includes('Failed to')) {
                    throw e;
                }
                throw new Error("Failed to change password");
            }
        }
    } catch (e) {
        throw e;
    }
};

export const logout = () => {
  localStorage.removeItem('nexus_token');
  localStorage.removeItem('nexus_user');
  localStorage.removeItem('nexus_active_view');
  localStorage.removeItem('nexus_active_workspace');
};

export const getToken = () => localStorage.getItem('nexus_token');

export const getUser = () => {
  const userStr = localStorage.getItem('nexus_user');
  return userStr ? JSON.parse(userStr) : null;
};
