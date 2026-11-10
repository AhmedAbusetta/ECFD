import React from "react";

/** A plain section: a heading and its content, separated from its neighbours by the parent's dividers. */
export const Panel: React.FC<{
  title?: string;
  aside?: React.ReactNode;
  className?: string;
  children: React.ReactNode;
}> = ({ title, aside, className = "", children }) => (
  <section className={`py-5 first:pt-0 last:pb-0 ${className}`}>
    {title && (
      <header className="mb-3 flex items-baseline justify-between gap-3">
        <h3 className="text-[13px] font-semibold">{title}</h3>
        {aside}
      </header>
    )}
    {children}
  </section>
);
