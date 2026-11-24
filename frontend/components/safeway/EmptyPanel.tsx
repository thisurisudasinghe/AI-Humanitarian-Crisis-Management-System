import type { ReactNode } from "react";

export function EmptyPanel({
  title,
  children
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <div className="empty-panel">
      <b>{title}</b>
      <p>{children}</p>
    </div>
  );
}
