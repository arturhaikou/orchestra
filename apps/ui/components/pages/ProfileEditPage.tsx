import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Save, Loader2, AlertTriangle, ArrowLeft, User as UserIcon, Mail, Lock, ShieldCheck, Eye, EyeOff } from 'lucide-react';
import { getUser, updateUser, changePassword } from '../../services/authService';
import { validatePassword } from '../../utils/passwordValidator';

const ProfileEditPage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  const [formState, setFormState] = useState({
    name: '',
    email: '',
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });
  const [showPasswords, setShowPasswords] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    const user = getUser();
    if (user) {
      setFormState(prev => ({
        ...prev,
        name: user.name,
        email: user.email,
      }));
    }
  }, []);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!formState.name.trim()) {
      setError('Name is required');
      return;
    }

    if (!formState.email.trim()) {
      setError('Email is required');
      return;
    }

    const isChangingPassword =
      formState.newPassword || formState.confirmPassword || formState.currentPassword;

    if (isChangingPassword) {
      if (!formState.currentPassword) {
        setError('Current password is required to make security changes.');
        return;
      }
      if (formState.newPassword !== formState.confirmPassword) {
        setError('New passwords do not match.');
        return;
      }
      const passwordValidation = validatePassword(formState.newPassword);
      if (!passwordValidation.isValid) {
        setError(passwordValidation.errors[0]);
        return;
      }
    }

    setIsSaving(true);
    try {
      await updateUser({ name: formState.name, email: formState.email });

      if (isChangingPassword) {
        await changePassword(formState.currentPassword, formState.newPassword);
      }

      navigate(`/workspaces/${workspaceId}/tickets`);
    } catch (err: any) {
      setError(err.message || 'Failed to update profile.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    navigate(-1);
  };

  return (
    <div className="max-w-3xl mx-auto">
      <div className="mb-6">
        <button
          onClick={handleCancel}
          className="text-sm text-textMuted hover:text-primary transition-colors flex items-center gap-1"
        >
          <ArrowLeft className="w-4 h-4" /> Back
        </button>
      </div>

      <h1 className="text-2xl font-bold text-text mb-6 flex items-center gap-2">
        <UserIcon className="w-6 h-6 text-primary" /> Profile Settings
      </h1>

      <form onSubmit={handleSave} className="space-y-6">
        {error && (
          <div className="p-3 bg-red-500/10 border border-red-500/20 rounded text-red-500 text-xs font-medium flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            {error}
          </div>
        )}

        <div className="space-y-4">
          <h2 className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-2">
            <UserIcon className="w-3.5 h-3.5" /> Personal Information
          </h2>
          <div className="grid grid-cols-1 gap-4">
            <div className="space-y-1.5">
              <label htmlFor="profile-name" className="text-[10px] font-semibold text-textMuted uppercase">
                Display Name
              </label>
              <div className="relative group">
                <UserIcon className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                <input
                  id="profile-name"
                  type="text"
                  value={formState.name}
                  onChange={(e) => setFormState(prev => ({ ...prev, name: e.target.value }))}
                  placeholder="John Doe"
                  disabled={isSaving}
                  className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all disabled:opacity-50"
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <label htmlFor="profile-email" className="text-[10px] font-semibold text-textMuted uppercase">
                Email Address
              </label>
              <div className="relative group">
                <Mail className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                <input
                  id="profile-email"
                  type="email"
                  value={formState.email}
                  onChange={(e) => setFormState(prev => ({ ...prev, email: e.target.value }))}
                  placeholder="john@example.com"
                  disabled={isSaving}
                  className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all disabled:opacity-50"
                />
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-4 pt-4 border-t border-border/50">
          <div className="flex items-center justify-between">
            <h2 className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-2">
              <ShieldCheck className="w-3.5 h-3.5" /> Security & Password
            </h2>
            <button
              type="button"
              onClick={() => setShowPasswords(!showPasswords)}
              className="text-[10px] font-bold text-primary hover:text-primaryHover transition-colors flex items-center gap-1"
            >
              {showPasswords ? <EyeOff className="w-3 h-3" /> : <Eye className="w-3 h-3" />}
              {showPasswords ? 'Hide Inputs' : 'Show Inputs'}
            </button>
          </div>

          <div className="space-y-4">
            <div className="space-y-1.5">
              <label htmlFor="profile-current-password" className="text-[10px] font-semibold text-textMuted uppercase">
                Current Password
              </label>
              <div className="relative group">
                <Lock className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                <input
                  id="profile-current-password"
                  type={showPasswords ? 'text' : 'password'}
                  value={formState.currentPassword}
                  onChange={(e) => setFormState(prev => ({ ...prev, currentPassword: e.target.value }))}
                  placeholder="Required for password changes"
                  disabled={isSaving}
                  className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all font-mono disabled:opacity-50"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label htmlFor="profile-new-password" className="text-[10px] font-semibold text-textMuted uppercase">
                  New Password
                </label>
                <div className="relative group">
                  <Lock className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                  <input
                    id="profile-new-password"
                    type={showPasswords ? 'text' : 'password'}
                    value={formState.newPassword}
                    onChange={(e) => setFormState(prev => ({ ...prev, newPassword: e.target.value }))}
                    placeholder="Min 8 chars, 1 uppercase, 1 digit, 1 special"
                    disabled={isSaving}
                    className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all font-mono disabled:opacity-50"
                  />
                </div>
              </div>
              <div className="space-y-1.5">
                <label htmlFor="profile-confirm-password" className="text-[10px] font-semibold text-textMuted uppercase">
                  Confirm New
                </label>
                <div className="relative group">
                  <Lock className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                  <input
                    id="profile-confirm-password"
                    type={showPasswords ? 'text' : 'password'}
                    value={formState.confirmPassword}
                    onChange={(e) => setFormState(prev => ({ ...prev, confirmPassword: e.target.value }))}
                    placeholder="Repeat new password"
                    disabled={isSaving}
                    className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all font-mono disabled:opacity-50"
                  />
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="pt-4 flex flex-col sm:flex-row gap-3">
          <button
            type="button"
            onClick={handleCancel}
            disabled={isSaving}
            className="flex-1 px-4 py-2.5 border border-border rounded-md text-sm font-bold uppercase tracking-widest text-textMuted hover:text-text hover:bg-surfaceHighlight transition-colors"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={isSaving}
            className="flex-1 px-4 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-bold uppercase tracking-widest transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-primary/20"
          >
            {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            {isSaving ? 'Saving...' : 'Save Profile'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default ProfileEditPage;
