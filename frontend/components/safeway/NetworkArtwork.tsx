import { Icon } from "./Icon";

export function NetworkArtwork() {
  const nodes: [string, string, string, string][] = [
    ["node-report", "alert", "Crisis reported", "Location verified"],
    ["node-shelter", "home", "Safe shelter", "Spaces available"],
    ["node-resource", "box", "Resources", "Ready to dispatch"]
  ];

  return (
    <div className="network-art" aria-label="Connected humanitarian response illustration">
      <div className="network-grid" />
      <div className="network-ring ring-a" />
      <div className="network-ring ring-b" />
      <div className="network-route route-a" />
      <div className="network-route route-b" />
      {nodes.map(([className, icon, title, copy]) => (
        <span className={`network-node ${className}`} key={className}>
          <i>
            <Icon name={icon} />
          </i>
          <b>{title}</b>
          <small>{copy}</small>
        </span>
      ))}
      <span className="network-core">
        <i>
          <Icon name="heart" size={28} />
        </i>
        <b>SAFEWAY</b>
        <small>Connected response</small>
      </span>
      <span className="network-pulse pulse-a" />
      <span className="network-pulse pulse-b" />
      <span className="network-coordinate">06°31&apos;N &nbsp; 03°22&apos;E</span>
    </div>
  );
}
