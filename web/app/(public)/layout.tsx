export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <div>
      <nav aria-label="Public information">
        <a href="/alerts">Public alerts</a>
        <a href="/service-centres">Service centres</a>
        <a href="/alerts#safety-guidance-heading">Safety guidance</a>
        <a href="/requests/new">Request assistance</a>
      </nav>
      {children}
    </div>
  );
}
