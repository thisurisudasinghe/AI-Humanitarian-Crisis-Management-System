export function LoadingState({ label = "Loading…" }: { label?: string }) {
  return <p className="muted" role="status">{label}</p>;
}

export function ErrorState({ message }: { message: string }) {
  return <p className="error" role="alert">{message}</p>;
}

export function EmptyState({ message }: { message: string }) {
  return <p className="muted">{message}</p>;
}
