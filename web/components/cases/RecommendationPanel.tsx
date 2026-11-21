"use client";

import { useEffect, useState } from "react";
import type { Recommendation } from "./casesApi";
import { casesApi } from "./casesApi";

export default function RecommendationPanel({ targetId }: { targetId: string }) {
  const [items, setItems] = useState<Recommendation[]>([]);
  const [message, setMessage] = useState("Loading M3 recommendations…");
  const [available, setAvailable] = useState(false);

  useEffect(() => {
    let active = true;
    casesApi
      .recommendationsByTarget(targetId)
      .then((result) => {
        if (!active) return;
        setItems(result);
        setAvailable(true);
        setMessage(result.length ? "" : "No recommendations for this request.");
      })
      .catch(() => {
        if (!active) return;
        setMessage(
          "M3 recommendation lookup is unavailable (GET /api/v1/recommendations?targetId=). " +
            "External dependency DEP-02 remains open."
        );
      });
    return () => {
      active = false;
    };
  }, [targetId]);

  return (
    <section className="panel" aria-labelledby="recommendations-heading">
      <h2 id="recommendations-heading">Recommendations</h2>
      {message ? <p className="muted">{message}</p> : null}
      {available ? (
        <ul>
          {items.map((item) => (
            <li key={item.id}>
              <strong>{item.type}</strong> · {Math.round(item.confidence * 100)}% confidence
              <div>{item.rationale}</div>
            </li>
          ))}
        </ul>
      ) : null}
      <p className="muted">
        The approval/decision form is Member 3 owned and is not duplicated here.
      </p>
    </section>
  );
}
