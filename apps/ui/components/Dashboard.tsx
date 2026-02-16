
import React, { useState, useEffect } from 'react';
import { Activity, Loader2, RefreshCw } from 'lucide-react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid
} from 'recharts';
import { getTickets } from '../services/ticketService';
import { getAgents } from '../services/agentService';
import { getJobs } from '../services/jobService';
import { Ticket, Agent, Job } from '../types';

interface DashboardProps {
  workspaceId: string;
  isDarkMode?: boolean;
}

const Dashboard: React.FC<DashboardProps> = ({ workspaceId, isDarkMode = true }) => {
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const fetchData = async () => {
    setIsLoading(true);
    try {
      const [ticketRes, agentRes, jobRes] = await Promise.all([
        getTickets(workspaceId, undefined, 100),
        getAgents(workspaceId),
        getJobs(workspaceId)
      ]);
      setTickets(ticketRes.items);
      setAgents(agentRes);
      setJobs(jobRes);
    } catch (error) {
      console.error("Dashboard failed to fetch live data", error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
    // Live update simulation every 30 seconds
    const interval = setInterval(fetchData, 30000);
    return () => clearInterval(interval);
  }, [workspaceId]);

  const stats = [
    { label: 'Active Tickets', value: tickets.filter(t => t.status.name !== 'Done').length, color: 'text-blue-400' },
    { label: 'Active Agents', value: agents.filter(a => a.status === 'BUSY').length, color: 'text-purple-400' },
    { label: 'Jobs Running', value: jobs.filter(j => j.status === 'IN_PROGRESS').length, color: 'text-emerald-400' },
    { label: 'Avg Satisfaction', value: tickets.length > 0 ? `${Math.round(tickets.reduce((acc, t) => acc + t.satisfaction, 0) / tickets.length)}%` : '0%', color: 'text-yellow-400' },
  ];

  const volumeMultiplier = Math.max(1, tickets.length / 3);
  const chartData = [
    { name: 'Mon', tickets: Math.floor(2 * volumeMultiplier), jobs: Math.floor(1 * volumeMultiplier) },
    { name: 'Tue', tickets: Math.floor(4 * volumeMultiplier), jobs: Math.floor(2 * volumeMultiplier) },
    { name: 'Wed', tickets: Math.floor(3 * volumeMultiplier), jobs: Math.floor(4 * volumeMultiplier) },
    { name: 'Thu', tickets: Math.floor(5 * volumeMultiplier), jobs: Math.floor(3 * volumeMultiplier) },
    { name: 'Fri', tickets: Math.floor(6 * volumeMultiplier), jobs: Math.floor(5 * volumeMultiplier) },
  ];

  const chartColors = {
      grid: isDarkMode ? '#27272a' : '#e5e7eb',
      text: isDarkMode ? '#a1a1aa' : '#71717a',
      tooltipBg: isDarkMode ? '#18181b' : '#ffffff',
      tooltipBorder: isDarkMode ? '#27272a' : '#e5e7eb',
      tooltipText: isDarkMode ? '#e4e4e7' : '#18181b',
  };

  if (isLoading && tickets.length === 0) {
    return (
        <div className="flex flex-col items-center justify-center h-full gap-4 text-textMuted">
            <Loader2 className="w-8 h-8 animate-spin text-primary" />
            <p className="text-sm font-mono animate-pulse">Fetching system intelligence data...</p>
        </div>
    );
  }

  return (
    <div className="space-y-6 animate-fade-in h-full flex flex-col">
      <div className="flex justify-between items-center shrink-0">
        <h2 className="text-2xl font-bold text-text">Dashboard</h2>
        <button 
            onClick={fetchData} 
            className="p-2 hover:bg-surfaceHighlight rounded-md text-textMuted hover:text-text transition-colors"
            title="Refresh Dashboard"
        >
            <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin text-primary' : ''}`} />
        </button>
      </div>
      
      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 shrink-0">
        {stats.map((stat, idx) => (
          <div key={idx} className="bg-surface border border-border p-5 rounded-lg shadow-sm hover:border-primary/30 transition-colors">
            <p className="text-textMuted text-[10px] font-bold uppercase tracking-widest">{stat.label}</p>
            <p className={`text-3xl font-mono font-semibold mt-2 ${stat.color}`}>{stat.value}</p>
          </div>
        ))}
      </div>

      {/* Main Content */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 flex-1 min-h-0">
        {/* Activity Chart */}
        <div className="lg:col-span-2 bg-surface border border-border p-6 rounded-lg flex flex-col min-h-[350px]">
          <h3 className="text-lg font-semibold text-text mb-4 shrink-0">Weekly Velocity</h3>
          <div className="flex-1 min-h-0">
            <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" stroke={chartColors.grid} />
                <XAxis dataKey="name" stroke={chartColors.text} />
                <YAxis stroke={chartColors.text} />
                <Tooltip 
                    contentStyle={{ 
                        backgroundColor: chartColors.tooltipBg, 
                        borderColor: chartColors.tooltipBorder, 
                        color: chartColors.tooltipText 
                    }} 
                />
                <Bar dataKey="tickets" fill="#6366f1" radius={[4, 4, 0, 0]} />
                <Bar dataKey="jobs" fill="#10b981" radius={[4, 4, 0, 0]} />
                </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Live Feed */}
        <div className="bg-surface border border-border p-6 rounded-lg min-h-[350px] overflow-hidden flex flex-col shadow-lg shadow-black/10">
          <h3 className="text-lg font-semibold text-text mb-4 flex items-center gap-2 shrink-0">
            <Activity className="w-4 h-4 text-emerald-400" /> System Feed
          </h3>
          <div className="flex-1 overflow-y-auto space-y-4 pr-2 custom-scrollbar">
            {jobs.length === 0 ? (
               <div className="text-textMuted text-sm italic text-center py-10 opacity-50">
                   No recent system activity.
               </div>
            ) : (
                jobs.flatMap(j => j.logs.map((l, i) => ({ log: l, jobId: j.id, type: j.type, i }))).slice(-20).reverse().map((item, idx) => (
                  <div key={idx} className="text-[11px] font-mono border-l-2 border-primary/30 pl-3 py-1.5 animate-in fade-in slide-in-from-left-2">
                    <div className="flex items-center gap-2 mb-0.5">
                        <span className="text-primaryHover font-bold">{item.jobId}</span>
                        {item.type && <span className="bg-surfaceHighlight px-1 rounded text-[9px] text-textMuted">{item.type}</span>}
                    </div>
                    <span className="text-textMuted leading-tight">{item.log}</span>
                  </div>
                ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
