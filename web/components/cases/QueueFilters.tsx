"use client";

import { CATEGORIES, REQUEST_STATUSES, URGENCIES } from "./casesApi";

export interface QueueFilterValues {
  status: string;
  urgency: string;
  category: string;
  crisisId: string;
  areaId: string;
  assignedTo: string;
  overdue: boolean;
  q: string;
  sortBy: string;
  sortDescending: boolean;
}

export const EMPTY_FILTERS: QueueFilterValues = {
  status: "",
  urgency: "",
  category: "",
  crisisId: "",
  areaId: "",
  assignedTo: "",
  overdue: false,
  q: "",
  sortBy: "submittedAt",
  sortDescending: true
};

export default function QueueFilters({
  value,
  onChange
}: {
  value: QueueFilterValues;
  onChange: (next: QueueFilterValues) => void;
}) {
  function set<K extends keyof QueueFilterValues>(key: K, next: QueueFilterValues[K]) {
    onChange({ ...value, [key]: next });
  }

  return (
    <section className="panel" aria-label="Request filters">
      <label htmlFor="filter-q">Search</label>
      <input id="filter-q" value={value.q} onChange={(event) => set("q", event.target.value)} />

      <label htmlFor="filter-status">Status</label>
      <select id="filter-status" value={value.status} onChange={(event) => set("status", event.target.value)}>
        <option value="">All</option>
        {REQUEST_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
      </select>

      <label htmlFor="filter-urgency">Urgency</label>
      <select id="filter-urgency" value={value.urgency} onChange={(event) => set("urgency", event.target.value)}>
        <option value="">All</option>
        {URGENCIES.map((urgency) => <option key={urgency} value={urgency}>{urgency}</option>)}
      </select>

      <label htmlFor="filter-category">Category</label>
      <select id="filter-category" value={value.category} onChange={(event) => set("category", event.target.value)}>
        <option value="">All</option>
        {CATEGORIES.map((category) => <option key={category} value={category}>{category}</option>)}
      </select>

      <label htmlFor="filter-crisis">Crisis ID</label>
      <input id="filter-crisis" value={value.crisisId} onChange={(event) => set("crisisId", event.target.value)} />

      <label htmlFor="filter-area">Area ID</label>
      <input id="filter-area" value={value.areaId} onChange={(event) => set("areaId", event.target.value)} />

      <label htmlFor="filter-assignee">Assignee user ID</label>
      <input id="filter-assignee" value={value.assignedTo} onChange={(event) => set("assignedTo", event.target.value)} />

      <label htmlFor="filter-overdue">
        <input
          id="filter-overdue"
          type="checkbox"
          checked={value.overdue}
          onChange={(event) => set("overdue", event.target.checked)}
        />{" "}
        Overdue only
      </label>

      <label htmlFor="filter-sort">Sort by</label>
      <select id="filter-sort" value={value.sortBy} onChange={(event) => set("sortBy", event.target.value)}>
        <option value="submittedAt">Submitted</option>
        <option value="status">Status</option>
        <option value="urgency">Urgency</option>
        <option value="slaAgeHours">SLA age</option>
      </select>

      <label htmlFor="filter-desc">
        <input
          id="filter-desc"
          type="checkbox"
          checked={value.sortDescending}
          onChange={(event) => set("sortDescending", event.target.checked)}
        />{" "}
        Descending
      </label>

      <p>
        <button type="button" onClick={() => onChange(EMPTY_FILTERS)}>Reset filters</button>
      </p>
    </section>
  );
}
