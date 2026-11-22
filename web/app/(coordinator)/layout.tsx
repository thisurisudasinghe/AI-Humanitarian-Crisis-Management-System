export default function CoordinatorLayout({ children }: { children: React.ReactNode }) {
  return (
    <div>
      <nav aria-label="Coordinator workspace">
        <a href="/dashboard">Dashboard</a>
        <a href="/coordinator/requests">Request queue</a>
        <a href="/crises">Crises and areas</a>
        <a href="/alerts">Public alerts</a>
      </nav>
      {children}
    </div>
  );
}
