"use client";

import Link from "next/link";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Icon } from "@/components/safeway/Icon";
import { VolunteerLocationPicker } from "@/components/safeway/VolunteerLocationPicker";
import { VolunteerPage } from "@/components/safeway/VolunteerPage";
import { PortalNotice, PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { useAuth } from "@/lib/auth";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import {
  getVolunteerPortal,
  saveVolunteerProfile,
  type VolunteerProfile
} from "@/lib/portal";
import {
  hasValidPoint,
  languageLabel,
  normalizeLanguageValue,
  normalizeVerificationStatus,
  parseBackendArray,
  serializeBackendArray,
  skillLabel,
  verificationTone,
  VOLUNTEER_LANGUAGES,
  VOLUNTEER_SKILLS
} from "@/lib/volunteer";
import type { MapPoint } from "@/components/map/MapPicker";

export default function VolunteerProfilePage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const [profile, setProfile] = useState<VolunteerProfile | null>(null);
  const [skills, setSkills] = useState<string[]>([]);
  const [customSkills, setCustomSkills] = useState<string[]>([]);
  const [languages, setLanguages] = useState<string[]>([]);
  const [active, setActive] = useState(true);
  const [lat, setLat] = useState<number | null>(null);
  const [lng, setLng] = useState<number | null>(null);
  const [customSkill, setCustomSkill] = useState("");
  const [showOther, setShowOther] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const applyProfile = useCallback((next: VolunteerProfile | null) => {
    setProfile(next);
    const savedSkills = parseBackendArray(next?.skillsJson);
    const standardValues = new Set<string>(VOLUNTEER_SKILLS.map((item) => item.value));
    const standard = savedSkills.filter((value) => standardValues.has(value));
    const custom = savedSkills.filter((value) => !standardValues.has(value));
    setSkills(standard);
    setCustomSkills(custom);
    setLanguages(
      parseBackendArray(next?.languagesJson).map((value) => normalizeLanguageValue(value))
    );
    setActive(next?.active ?? true);
    setLat(hasValidPoint(next?.lat, next?.lng) ? next?.lat ?? null : null);
    setLng(hasValidPoint(next?.lat, next?.lng) ? next?.lng ?? null : null);
    setShowOther(custom.length > 0);
  }, []);

  useEffect(() => {
    let activeRequest = true;
    getVolunteerPortal()
      .then((result) => {
        if (!activeRequest) return;
        applyProfile(result.profile);
        setLoadError(null);
      })
      .catch((cause) => {
        if (activeRequest) setLoadError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (activeRequest) setLoading(false);
      });

    return () => {
      activeRequest = false;
    };
  }, [applyProfile, t]);

  function toggleSkill(value: string) {
    setSkills((current) =>
      current.includes(value) ? current.filter((item) => item !== value) : [...current, value]
    );
  }

  function toggleLanguage(value: string) {
    setLanguages((current) =>
      current.includes(value) ? current.filter((item) => item !== value) : [...current, value]
    );
  }

  function addCustomSkill() {
    const value = customSkill.trim();
    if (!value) {
      setFormError(t("volunteerX.profile.customSkillRequired"));
      return;
    }
    if (!customSkills.some((item) => item.toLowerCase() === value.toLowerCase())) {
      setCustomSkills((current) => [...current, value]);
    }
    setCustomSkill("");
    setFormError(null);
  }

  function onLocationChange(point: MapPoint | null) {
    setLat(point?.lat ?? null);
    setLng(point?.lng ?? null);
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    setSaving(true);
    setFormError(null);
    setNotice(null);

    try {
      const saved = await saveVolunteerProfile({
        skillsJson: serializeBackendArray([...skills, ...customSkills]),
        languagesJson: serializeBackendArray(languages),
        lat,
        lng,
        active
      });
      applyProfile(saved);
      setNotice(t("volunteerX.profile.success"));
    } catch (cause) {
      setFormError(localizedErrorMessage(cause, t));
    } finally {
      setSaving(false);
    }
  }

  const status = normalizeVerificationStatus(profile?.verificationStatus, Boolean(profile));
  const allSkillValues = [...skills, ...customSkills];

  return (
    <VolunteerPage
      eyebrowKey="volunteerX.profile.title"
      titleKey="volunteerX.profile.title"
      descriptionKey="volunteerX.profile.description"
      action={
        <span className={`badge ${verificationTone(status)}`}>
          <i aria-hidden="true" /> {status === "None" ? t("volunteerX.profile.statusLabel") : t(`volunteerX.shared.status.${status}`)}
        </span>
      }
    >
      {notice ? <PortalNotice>{notice}</PortalNotice> : null}
      {formError ? <PortalNotice tone="error">{formError}</PortalNotice> : null}

      {loading ? (
        <PortalState kind="loading" title={t("volunteerX.profile.loadingTitle")}>
          {t("volunteerX.profile.loadingBody")}
        </PortalState>
      ) : loadError ? (
        <PortalState kind="error" title={t("volunteerX.profile.errorTitle")}>
          {loadError}
        </PortalState>
      ) : (
        <form className="volunteer-profile-form" onSubmit={(event) => void onSubmit(event)} noValidate>
          <PortalSection
            title={t("volunteerX.profile.personalTitle")}
            description={t("volunteerX.profile.personalDescription")}
          >
            <dl className="volunteer-facts">
              <div>
                <dt><Icon name="user" size={16} /> {t("volunteerX.profile.nameLabel")}</dt>
                <dd>{user?.fullName ?? "—"}</dd>
              </div>
              <div>
                <dt><Icon name="mail" size={16} /> {t("volunteerX.profile.emailLabel")}</dt>
                <dd>{user?.email ?? "—"}</dd>
              </div>
            </dl>
          </PortalSection>

          <PortalSection
            title={t("volunteerX.profile.skillsTitle")}
            description={t("volunteerX.profile.skillsDescription")}
          >
            <div className="volunteer-chip-grid" role="group" aria-label={t("volunteerX.profile.skillsTitle")}>
              {VOLUNTEER_SKILLS.map((item) => {
                const selected = skills.includes(item.value);
                return (
                  <button
                    className={`volunteer-chip${selected ? " is-selected" : ""}`}
                    type="button"
                    key={item.value}
                    aria-pressed={selected}
                    onClick={() => toggleSkill(item.value)}
                  >
                    <Icon name={item.icon} size={16} />
                    <span>{t(item.labelKey)}</span>
                    {selected ? <Icon name="check" size={14} /> : null}
                  </button>
                );
              })}
              <button
                className={`volunteer-chip${showOther ? " is-selected" : ""}`}
                type="button"
                aria-pressed={showOther}
                onClick={() => setShowOther((value) => !value)}
              >
                <Icon name="plus" size={16} />
                <span>{t("volunteerX.shared.skill.other")}</span>
              </button>
            </div>

            {customSkills.length > 0 ? (
              <div className="volunteer-custom-list" aria-label={t("volunteerX.profile.customSkillLabel")}>
                {customSkills.map((skill) => (
                  <span className="volunteer-chip is-selected is-custom" key={skill}>
                    <Icon name="checkCircle" size={15} />
                    <span>{skill}</span>
                    <button
                      type="button"
                      aria-label={t("volunteerX.profile.removeSkill", { skill })}
                      onClick={() => setCustomSkills((current) => current.filter((item) => item !== skill))}
                    >
                      <Icon name="close" size={13} />
                    </button>
                  </span>
                ))}
              </div>
            ) : null}

            {showOther ? (
              <div className="volunteer-inline-form">
                <label htmlFor="volunteer-custom-skill">{t("volunteerX.profile.customSkillLabel")}</label>
                <input
                  id="volunteer-custom-skill"
                  type="text"
                  value={customSkill}
                  placeholder={t("volunteerX.profile.customSkillPlaceholder")}
                  onChange={(event) => setCustomSkill(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      event.preventDefault();
                      addCustomSkill();
                    }
                  }}
                />
                <button className="btn btn-outline" type="button" onClick={addCustomSkill}>
                  {t("volunteerX.profile.addSkill")}
                </button>
              </div>
            ) : null}

            <p className="volunteer-field-help">
              <Icon name="info" size={14} /> {t("volunteerX.profile.skillsHelp")}
            </p>
          </PortalSection>

          <PortalSection
            title={t("volunteerX.profile.languagesTitle")}
            description={t("volunteerX.profile.languagesDescription")}
          >
            <div className="volunteer-chip-grid" role="group" aria-label={t("volunteerX.profile.languagesTitle")}>
              {VOLUNTEER_LANGUAGES.map((item) => {
                const selected = languages.includes(item.value);
                return (
                  <button
                    className={`volunteer-chip${selected ? " is-selected" : ""}`}
                    type="button"
                    key={item.value}
                    aria-pressed={selected}
                    onClick={() => toggleLanguage(item.value)}
                  >
                    <Icon name="checkCircle" size={16} />
                    <span>{t(item.labelKey)}</span>
                    {selected ? <Icon name="check" size={14} /> : null}
                  </button>
                );
              })}
              {languages
                .filter((value) => !VOLUNTEER_LANGUAGES.some((item) => item.value === value))
                .map((value) => (
                  <span className="volunteer-chip is-selected is-custom" key={value}>
                    <span>{languageLabel(t, value)}</span>
                    <button
                      type="button"
                      aria-label={t("volunteerX.profile.removeSkill", { skill: value })}
                      onClick={() => setLanguages((current) => current.filter((item) => item !== value))}
                    >
                      <Icon name="close" size={13} />
                    </button>
                  </span>
                ))}
            </div>
          </PortalSection>

          <PortalSection
            title={t("volunteerX.profile.experienceTitle")}
            description={t("volunteerX.profile.experienceDescription")}
          >
            <p className="volunteer-field-help">{t("volunteerX.profile.experienceBody")}</p>
            <p className="volunteer-field-help">
              {allSkillValues.length > 0
                ? t("volunteerX.shared.skillsLabel", { skills: allSkillValues.map((skill) => skillLabel(t, skill)).join(", ") })
                : t("volunteerX.opportunity.noSkills")}
            </p>
          </PortalSection>

          <PortalSection
            title={t("volunteerX.profile.locationTitle")}
            description={t("volunteerX.profile.locationDescription")}
          >
            <VolunteerLocationPicker lat={lat} lng={lng} onChange={onLocationChange} />
          </PortalSection>

          <PortalSection
            title={t("volunteerX.profile.availabilityControlTitle")}
            description={t("volunteerX.landing.cardAvailabilityCopy")}
          >
            <label className="volunteer-check" htmlFor="volunteer-active">
              <input
                id="volunteer-active"
                type="checkbox"
                checked={active}
                onChange={(event) => setActive(event.target.checked)}
              />
              <span>{t("volunteerX.profile.availableLabel")}</span>
            </label>
            <p className="volunteer-field-help">
              {status === "Verified"
                ? t("volunteerX.verifyNote.verifiedActive")
                : t("volunteerX.verifyNote.pending")}
            </p>
          </PortalSection>

          <div className="volunteer-form-actions">
            <button className="btn btn-primary" type="submit" disabled={saving} aria-busy={saving}>
              {saving ? t("volunteerX.profile.saving") : t("volunteerX.profile.save")}
              {saving ? null : <Icon name="check" size={16} />}
            </button>
            <Link className="btn btn-outline" href="/volunteer/verification">
              {t("volunteerX.profile.verificationLink")} <Icon name="arrow" size={16} />
            </Link>
          </div>
        </form>
      )}
    </VolunteerPage>
  );
}
