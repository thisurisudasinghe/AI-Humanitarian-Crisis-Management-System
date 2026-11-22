export default function CitizenLayout({ children }: { children: React.ReactNode }) {
  return (
    <div>
      <nav aria-label="Citizen workspace">
        <a href="/requests">My requests</a>
        <a href="/requests/new">New request</a>
        <a href="/profile">My household</a>
        <a href="/alerts">Public alerts</a>
      </nav>
      {children}
    </div>
  );
}
