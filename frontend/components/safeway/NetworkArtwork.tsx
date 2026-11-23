"use client";

import { useT } from "@/lib/i18n";
import { Icon } from "./Icon";

const nodes = [
  {
    key: "report",
    icon: "alert",
    labelKey: "shell.networkNodeReportingLabel",
    detailKey: "shell.networkNodeReportingDetail"
  },
  {
    key: "coordination",
    icon: "brain",
    labelKey: "shell.networkNodeCoordinationLabel",
    detailKey: "shell.networkNodeCoordinationDetail"
  },
  {
    key: "shelters",
    icon: "home",
    labelKey: "shell.networkNodeSheltersLabel",
    detailKey: "shell.networkNodeSheltersDetail"
  },
  {
    key: "resources",
    icon: "box",
    labelKey: "shell.networkNodeResourcesLabel",
    detailKey: "shell.networkNodeResourcesDetail"
  },
  {
    key: "volunteers",
    icon: "users",
    labelKey: "shell.networkNodeVolunteersLabel",
    detailKey: "shell.networkNodeVolunteersDetail"
  }
] as const;

export function NetworkArtwork() {
  const t = useT();
  return (
    <div
      className="public-network"
      role="img"
      aria-label={t("shell.networkAria")}
    >
      <svg
        className="public-network__canvas"
        viewBox="0 0 800 420"
        preserveAspectRatio="xMidYMid meet"
        aria-hidden="true"
        focusable="false"
      >
        <circle className="public-network__halo" cx="400" cy="210" r="146" />
        <g className="public-network__orbit">
          <circle className="public-network__orbit-ring" cx="400" cy="210" r="188" />
          <circle className="public-network__orbit-dot" cx="400" cy="22" r="4" />
        </g>
        <g className="public-network__spokes">
          <line x1="400" y1="210" x2="165" y2="105" />
          <line x1="400" y1="210" x2="635" y2="105" />
          <line x1="400" y1="210" x2="120" y2="245" />
          <line x1="400" y1="210" x2="680" y2="245" />
          <line x1="400" y1="210" x2="400" y2="360" />
        </g>
        <g className="public-network__signals">
          <circle cx="400" cy="210" r="5" />
          <circle className="public-network__signal--delay" cx="400" cy="210" r="3" />
        </g>
      </svg>

      <div className="public-network__core">
        <span className="public-network__heart">
          <Icon name="heart" size={28} />
        </span>
        <b>SAFEWAY</b>
        <small>{t("shell.networkConnectedResponse")}</small>
      </div>

      <ul className="public-network__nodes">
        {nodes.map((node) => (
          <li key={node.key} className={`public-network__node public-network__node--${node.key}`}>
            <span className="public-network__node-icon">
              <Icon name={node.icon} size={18} />
            </span>
            <span className="public-network__node-copy">
              <b>{t(node.labelKey)}</b>
              <small>{t(node.detailKey)}</small>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
