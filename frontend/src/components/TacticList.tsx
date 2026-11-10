import React from "react";
import { TacticEvidence } from "@/types";
import { describeTactic } from "./labels";
import { Panel } from "./Panel";

interface TacticListProps {
  tactics: TacticEvidence[];
}

export const TacticList: React.FC<TacticListProps> = ({ tactics }) => (
  <Panel title="What the caller asked for">
    {tactics.length === 0 ? (
      <p className="text-sm text-faint">Nothing forbidden so far.</p>
    ) : (
      <ul className="space-y-1.5">
        {tactics.map((t, i) => (
          <li key={i} className="flex justify-between gap-4 text-sm">
            <span>The caller {describeTactic(t.tactic)}</span>
            <span className="text-faint tabular-nums shrink-0">{new Date(t.timestamp).toLocaleTimeString()}</span>
          </li>
        ))}
      </ul>
    )}
  </Panel>
);
