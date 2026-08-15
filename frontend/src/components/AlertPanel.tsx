import React from "react";
import { AlertEvent } from "@/types";

interface AlertPanelProps {
  alerts: AlertEvent[];
}

export const AlertPanel: React.FC<AlertPanelProps> = ({ alerts }) => {
  if (alerts.length === 0) return null;

  return (
    <div className="w-full space-y-3 mb-6">
      {alerts.map((a, i) => (
        <div key={i} className="bg-red-950/80 border-2 border-red-600/80 rounded-xl p-4 flex items-center justify-between shadow-2xl animate-pulse">
          <div className="flex items-center space-x-4">
            <span className="text-2xl">🚨</span>
            <div>
              <h4 className="text-sm font-bold text-red-200">{a.title}</h4>
              <p className="text-xs text-red-300/80">{a.description}</p>
            </div>
          </div>
          <span className="text-xs font-mono font-bold bg-red-600 text-white px-3 py-1 rounded">
            {a.severity.toUpperCase()}
          </span>
        </div>
      ))}
    </div>
  );
};
