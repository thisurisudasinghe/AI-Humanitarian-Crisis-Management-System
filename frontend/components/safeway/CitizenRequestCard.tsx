"use client";

import Link from "next/link";
import { statusTone } from "@/lib/citizen-status";
import type { CaseRequestDetail, CaseRequestListItem } from "@/lib/crisis";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { Icon } from "./Icon";

export function CitizenRequestCard({
  report,
  detail
}: {
  report: CaseRequestListItem;
  detail?: CaseRequestDetail | null;
}) {
  const { t, locale } = useI18n();
  const latest = detail?.timeline?.[detail.timeline.length - 1];
  const location = detail?.address?.trim();

  return (
    <article className="citizen-request-card">
      <div className="citizen-request-card__top">
        <span className="citizen-request-card__icon" aria-hidden="true">
          <Icon name="clipboard" size={20} />
        </span>
        <div className="citizen-request-card__heading">
          <h3>{t(`citizenX.category.${report.category}`)}</h3>
          <p className="citizen-request-card__ref">
            {t("citizenX.tracking.reference")}: <span title={report.id}>{report.id}</span>
          </p>
        </div>
        <span className={`badge ${statusTone(report.status)}`}>
          <i aria-hidden="true" /> {t(`citizenX.status.${report.status}`)}
        </span>
      </div>

      <p className="citizen-request-card__description">{report.description}</p>

      <dl className="citizen-request-card__meta">
        <div>
          <dt>{t("citizenX.tracking.submitted")}</dt>
          <dd>{formatDate(report.submittedAt, locale)}</dd>
        </div>
        <div>
          <dt>{t("citizenX.tracking.location")}</dt>
          <dd>{location || t("citizenX.tracking.notProvided")}</dd>
        </div>
        <div>
          <dt>{t("citizenX.tracking.requestedAssistance")}</dt>
          <dd>{t(`citizenX.category.${report.category}`)}</dd>
        </div>
      </dl>

      {latest ? (
        <p className="citizen-request-card__update">
          <Icon name="bell" size={15} /> <strong>{t("citizenX.tracking.latestUpdate")}:</strong>{" "}
          {t(`citizenX.status.${latest.toStatus}`)} · {formatDate(latest.createdAt, locale)}
        </p>
      ) : null}

      <Link className="citizen-request-card__link" href={`/my-reports/${report.id}`}>
        {t("citizenX.tracking.viewDetails")} <Icon name="arrow" size={16} />
      </Link>
    </article>
  );
}
