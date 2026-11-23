import type { ReactNode } from "react";

/**
 * Icon set carried over from the SAFEWAY Figma export.
 * Kept as a local component so the design has no icon-library dependency.
 */
export function Icon({
  name,
  size = 20,
  stroke = 1.8
}: {
  name: string;
  size?: number;
  stroke?: number;
}) {
  const paths: Record<string, ReactNode> = {
    arrow: <path d="M5 12h14m-5-5 5 5-5 5" />,
    shield: <path d="M12 3 5 6v5c0 4.4 2.8 8 7 10 4.2-2 7-5.6 7-10V6l-7-3Zm-3 9 2 2 4-5" />,
    heart: (
      <path d="M20.8 5.7c-1.8-2-5-2-6.8.1L12 8l-2-2.2c-1.8-2-5-2.1-6.8-.1-1.7 1.9-1.5 4.8.3 6.6L12 21l8.5-8.7c1.8-1.8 2-4.7.3-6.6Z" />
    ),
    pin: (
      <path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z M9 10a3 3 0 1 0 6 0 3 3 0 0 0-6 0Z" />
    ),
    bell: <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9ZM10 21h4" />,
    user: <path d="M20 21a8 8 0 0 0-16 0M12 13a5 5 0 1 0 0-10 5 5 0 0 0 0 10Z" />,
    menu: <path d="M4 7h16M4 12h16M4 17h16" />,
    close: <path d="m6 6 12 12M18 6 6 18" />,
    check: <path d="m5 12 4 4L19 6" />,
    phone: (
      <path d="M22 16.9v3a2 2 0 0 1-2.2 2c-8.5-1-15.1-7.6-16-16A2 2 0 0 1 5.8 3h3a2 2 0 0 1 2 1.7c.1 1 .4 2 .7 2.9a2 2 0 0 1-.5 2.1L9.8 11a16 16 0 0 0 3.2 3.2l1.3-1.2a2 2 0 0 1 2.1-.5c.9.3 1.9.6 2.9.7a2 2 0 0 1 1.7 2Z" />
    ),
    search: <path d="m21 21-4.3-4.3M19 11a8 8 0 1 1-16 0 8 8 0 0 1 16 0Z" />,
    file: (
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6Zm0 0v6h6M8 13h8M8 17h5" />
    ),
    home: <path d="m3 11 9-8 9 8v10h-6v-6H9v6H3V11Z" />,
    grid: <path d="M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z" />,
    alert: <path d="M12 3 2 21h20L12 3Zm0 6v5m0 3v.1" />,
    box: <path d="m21 8-9 5-9-5m9 5v9M4 6l8-4 8 4v12l-8 4-8-4V6Z" />,
    users: (
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm13 10v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8" />
    ),
    chart: <path d="M4 20V10m6 10V4m6 16v-7m5 7H2" />,
    brain: (
      <path d="M9.5 4.5A3.5 3.5 0 0 0 6 8v.3A3.5 3.5 0 0 0 7.5 15v1a3 3 0 0 0 5 2.2V5.8a3 3 0 0 0-3-1.3Zm5 0A3.5 3.5 0 0 1 18 8v.3a3.5 3.5 0 0 1-1.5 6.7v1a3 3 0 0 1-5 2.2" />
    ),
    chevron: <path d="m9 18 6-6-6-6" />,
    logout: <path d="M10 17l5-5-5-5m5 5H3m11-9h5a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-5" />,
    map: <path d="m3 6 6-3 6 3 6-3v15l-6 3-6-3-6 3V6Zm6-3v15m6-12v15" />,
    clock: <path d="M12 7v5l3 2m6-2a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />,
    upload: <path d="M12 16V4m-5 5 5-5 5 5M5 20h14" />,
    eye: (
      <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6-10-6-10-6Zm10 3a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" />
    ),
    eyeOff: (
      <path d="M3 3l18 18M10.6 10.7a2 2 0 0 0 2.7 2.7M9.5 5.1A11 11 0 0 1 12 5c6.5 0 10 7 10 7a15.6 15.6 0 0 1-4.1 4.4M6.2 6.3C3.3 8.2 2 12 2 12s3.5 7 10 7a11 11 0 0 0 5.4-1.3" />
    ),
    info: (
      <>
        <circle cx="12" cy="12" r="9" />
        <path d="M12 11v5m0-8v.1" />
      </>
    ),
    layers: (
      <>
        <path d="m12 3 9 5-9 5-9-5 9-5Z" />
        <path d="m3 12 9 5 9-5M3 16l9 5 9-5" />
      </>
    ),
    route: (
      <>
        <circle cx="6" cy="18" r="3" />
        <path d="M6 15V9a4 4 0 0 1 4-4h5" />
        <circle cx="18" cy="5" r="3" />
      </>
    ),
    mail: (
      <>
        <rect x="3" y="5" width="18" height="14" rx="2" />
        <path d="m3 7 9 6 9-6" />
      </>
    ),
    "help-circle": (
      <>
        <circle cx="12" cy="12" r="9" />
        <path d="M9.6 9.2a2.6 2.6 0 0 1 5 .9c0 1.7-2.6 2-2.6 4m0 3v.1" />
      </>
    )
  };

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={stroke}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {paths[name] ?? paths.shield}
    </svg>
  );
}
