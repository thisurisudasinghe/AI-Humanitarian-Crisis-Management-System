"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "@/lib/auth";
import { DonationLocationPicker, type DonorLocationValue } from "@/components/safeway/DonationLocationPicker";
import { DonorPage } from "@/components/safeway/DonorPage";
import { Icon } from "@/components/safeway/Icon";
import { PortalNotice, PortalState } from "@/components/safeway/PortalPrimitives";
import {
  DONOR_CATEGORIES,
  DONOR_UNITS,
  DELIVERY_METHODS,
  SUGGESTED_AMOUNTS,
  SUPPORTED_CURRENCIES,
  categoryIcon,
  categoryLabel,
  deliveryLabel,
  donationStatusLabel,
  donationStatusTone,
  donationSummary,
  hasValidPoint,
  localizeDonationError,
  newIdempotencyKey,
  unitLabel,
  utcDateInputValue
} from "@/lib/donor";
import { formatMoney, type DonationDto } from "@/lib/donations";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useT } from "@/lib/i18n";
import {
  getDonorCampaigns,
  recordDonation,
  type CampaignDto,
  type DonationItemInput,
  type RecordDonationInput
} from "@/lib/portal";

type Choice = "Monetary" | "Material";
type Currency = (typeof SUPPORTED_CURRENCIES)[number];

interface ItemDraft {
  key: string;
  name: string;
  category: string;
  customCategory: string;
  quantity: string;
  unit: string;
  customUnit: string;
  condition: string;
  notes: string;
  expiryDate: string;
  packaging: string;
}

interface Draft {
  step: number;
  type: Choice | "";
  currency: Currency;
  amount: string;
  campaignId: string;
  notes: string;
  estimatedValue: string;
  items: ItemDraft[];
  lat: number | null;
  lng: number | null;
  locationLabel: string;
  locationAddress: string;
  deliveryMethod: string;
  idempotencyKey: string;
}

const STEP_KEYS = [
  "donorX.donate.stepType",
  "donorX.donate.stepDetails",
  "donorX.donate.stepLocation",
  "donorX.donate.stepReview"
] as const;

const CATEGORY_VALUES = DONOR_CATEGORIES.map((option) => option.value);
const UNIT_VALUES = DONOR_UNITS.map((option) => option.value);

function blankItem(): ItemDraft {
  return {
    key: newIdempotencyKey().replace("donor-", "item-"),
    name: "",
    category: "",
    customCategory: "",
    quantity: "1",
    unit: "",
    customUnit: "",
    condition: "",
    notes: "",
    expiryDate: "",
    packaging: ""
  };
}

function blankDraft(): Draft {
  return {
    step: 0,
    type: "",
    currency: "LKR",
    amount: "",
    campaignId: "",
    notes: "",
    estimatedValue: "",
    items: [],
    lat: null,
    lng: null,
    locationLabel: "",
    locationAddress: "",
    deliveryMethod: "",
    idempotencyKey: newIdempotencyKey()
  };
}

function readDraft(key: string): Draft {
  const base = blankDraft();
  if (typeof window === "undefined") return base;
  try {
    const saved = window.sessionStorage.getItem(key);
    if (!saved) return base;
    const parsed = JSON.parse(saved) as Partial<Draft>;
    return {
      ...base,
      ...parsed,
      step: typeof parsed.step === "number" && parsed.step >= 0 && parsed.step <= 3 ? parsed.step : 0,
      type: parsed.type === "Monetary" || parsed.type === "Material" ? parsed.type : "",
      currency: parsed.currency === "USD" ? "USD" : "LKR",
      items: Array.isArray(parsed.items)
        ? parsed.items.map((item) => ({ ...blankItem(), ...item }))
        : [],
      lat: typeof parsed.lat === "number" ? parsed.lat : null,
      lng: typeof parsed.lng === "number" ? parsed.lng : null,
      idempotencyKey: parsed.idempotencyKey || base.idempotencyKey
    };
  } catch {
    return base;
  }
}

function isSupportedCurrency(value: string | null): value is Currency {
  return value === "LKR" || value === "USD";
}

function decimalValue(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

function isValidQuantity(value: string): boolean {
  return /^\d+$/.test(value.trim()) && Number(value.trim()) > 0 && Number(value.trim()) <= 2_147_483_647;
}

export default function DonorDonatePage() {
  const t = useT();
  const { user } = useAuth();
  const draftKey = `safeway.donor.donationDraft.${user?.userId ?? "anonymous"}`;
  const [draft, setDraft] = useState<Draft>(blankDraft);
  const [ready, setReady] = useState(false);
  const [campaigns, setCampaigns] = useState<CampaignDto[]>([]);
  const [campaignState, setCampaignState] = useState<"loading" | "ready" | "error">("loading");
  const [campaignError, setCampaignError] = useState<string | null>(null);
  const [itemForm, setItemForm] = useState<ItemDraft>(blankItem);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [itemError, setItemError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [saved, setSaved] = useState<DonationDto | null>(null);
  const currencyPrefillRef = useRef<string | null>(null);

  useEffect(() => {
    const loaded = readDraft(draftKey);
    const params = new URLSearchParams(window.location.search);
    const campaignParam = params.get("campaign");
    const currencyParam = params.get("currency");

    if (campaignParam) loaded.campaignId = campaignParam;
    if (isSupportedCurrency(currencyParam)) loaded.currency = currencyParam;
    else if (campaignParam) currencyPrefillRef.current = campaignParam;

    setDraft(loaded);
    setReady(true);
  }, [draftKey]);

  useEffect(() => {
    if (!ready || saved) return;
    try {
      window.sessionStorage.setItem(draftKey, JSON.stringify(draft));
    } catch {
      // Draft persistence is a convenience; recording still works without it.
    }
  }, [draft, draftKey, ready, saved]);

  useEffect(() => {
    let active = true;
    setCampaignState("loading");
    getDonorCampaigns(1, 50)
      .then((result) => {
        if (!active) return;
        setCampaigns(result.items);
        const prefillId = currencyPrefillRef.current;
        if (prefillId) {
          const prefilled = result.items.find((campaign) => campaign.id === prefillId);
          if (prefilled && isSupportedCurrency(prefilled.currency.toUpperCase())) {
            setDraft((current) =>
              current.campaignId === prefillId
                ? { ...current, currency: prefilled.currency.toUpperCase() as Currency }
                : current
            );
          } else {
            setDraft((current) =>
              current.campaignId === prefillId ? { ...current, campaignId: "" } : current
            );
          }
          currencyPrefillRef.current = null;
        } else {
          setDraft((current) => {
            const campaign = result.items.find((candidate) => candidate.id === current.campaignId);
            if (current.campaignId && (!campaign || campaign.currency.toUpperCase() !== current.currency)) {
              return { ...current, campaignId: "" };
            }
            return current;
          });
        }
        setCampaignState("ready");
        setCampaignError(null);
      })
      .catch((cause) => {
        if (!active) return;
        setCampaignState("error");
        setCampaignError(localizedErrorMessage(cause, t));
      });
    return () => {
      active = false;
    };
  }, [t]);

  const visibleCampaigns = useMemo(
    () => campaigns.filter((campaign) => campaign.currency.toUpperCase() === draft.currency),
    [campaigns, draft.currency]
  );

  const selectedCampaign = campaigns.find((campaign) => campaign.id === draft.campaignId) ?? null;

  function updateDraft<K extends keyof Draft>(field: K, value: Draft[K]) {
    setDraft((current) => ({
      ...current,
      [field]: value,
      idempotencyKey: newIdempotencyKey()
    }));
    setFormError(null);
  }

  function changeStep(step: number) {
    setDraft((current) => ({ ...current, step: Math.max(0, Math.min(3, step)) }));
    setFormError(null);
    if (typeof window !== "undefined") window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function changeCurrency(next: Currency) {
    setDraft((current) => {
      const campaign = campaigns.find((candidate) => candidate.id === current.campaignId);
      const keepCampaign = campaign && campaign.currency.toUpperCase() === next;
      return {
        ...current,
        currency: next,
        campaignId: keepCampaign ? current.campaignId : "",
        idempotencyKey: newIdempotencyKey()
      };
    });
    setFormError(null);
  }

  function changeCampaign(id: string) {
    const campaign = campaigns.find((candidate) => candidate.id === id);
    setDraft((current) => ({
      ...current,
      campaignId: id,
      currency:
        campaign && isSupportedCurrency(campaign.currency.toUpperCase())
          ? (campaign.currency.toUpperCase() as Currency)
          : current.currency,
      idempotencyKey: newIdempotencyKey()
    }));
    setFormError(null);
  }

  function changeLocation(value: DonorLocationValue | null) {
    setDraft((current) => ({
      ...current,
      lat: value?.lat ?? null,
      lng: value?.lng ?? null,
      locationLabel: value?.label ?? "",
      locationAddress: value?.address ?? "",
      idempotencyKey: newIdempotencyKey()
    }));
    setFormError(null);
  }

  function changeItemField<K extends keyof ItemDraft>(field: K, value: ItemDraft[K]) {
    setItemForm((current) => ({ ...current, [field]: value }));
    setItemError(null);
  }

  function validateItem(item: ItemDraft): string | null {
    const name = item.name.trim();
    if (!name) return t("donorX.validation.itemName");
    if (name.length > 200) return t("donorX.validation.textTooLong");
    if (!isValidQuantity(item.quantity)) return t("donorX.validation.quantity");
    if (!CATEGORY_VALUES.includes(item.category as (typeof CATEGORY_VALUES)[number])) {
      return t("donorX.validation.categoryRequired");
    }
    if (item.category === "Other") {
      if (!item.customCategory.trim()) return t("donorX.validation.customCategory");
      if (item.customCategory.length > 80) return t("donorX.validation.textTooLong");
    }
    if (!UNIT_VALUES.includes(item.unit as (typeof UNIT_VALUES)[number])) {
      return t("donorX.validation.unitRequired");
    }
    if (item.unit === "Other") {
      if (!item.customUnit.trim()) return t("donorX.validation.customUnit");
      if (item.customUnit.length > 80) return t("donorX.validation.textTooLong");
    }
    if (item.condition.length > 100 || item.notes.length > 500 || item.packaging.length > 200) {
      return t("donorX.validation.textTooLong");
    }
    if (item.expiryDate && item.expiryDate < utcDateInputValue()) {
      return t("donorX.validation.expiryPast");
    }
    return null;
  }

  function commitItem() {
    const error = validateItem(itemForm);
    if (error) {
      setItemError(error);
      return;
    }
    const nextItems = [...draft.items];
    if (editingIndex === null) nextItems.push(itemForm);
    else nextItems[editingIndex] = itemForm;
    updateDraft("items", nextItems);
    setItemForm(blankItem());
    setEditingIndex(null);
    setItemError(null);
  }

  function editItem(index: number) {
    setItemForm({ ...draft.items[index] });
    setEditingIndex(index);
    setItemError(null);
    if (typeof window !== "undefined") window.scrollTo({ top: 260, behavior: "smooth" });
  }

  function removeItem(index: number) {
    updateDraft(
      "items",
      draft.items.filter((_, itemIndex) => itemIndex !== index)
    );
    if (editingIndex === index) {
      setItemForm(blankItem());
      setEditingIndex(null);
    } else if (editingIndex !== null && editingIndex > index) {
      setEditingIndex(editingIndex - 1);
    }
    setItemError(null);
  }

  function validateStep(step: number): string | null {
    if (step === 0) {
      return draft.type ? null : t("donorX.validation.selectDonationType");
    }

    if (step === 1) {
      if (!isSupportedCurrency(draft.currency)) return t("donorX.validation.currencyCode");
      if (draft.type === "Monetary") {
        const amount = decimalValue(draft.amount);
        if (amount === null) return t("donorX.validation.invalidAmount");
        if (amount <= 0) return t("donorX.validation.positiveAmount");
        if (draft.notes.length > 1000) return t("donorX.validation.textTooLong");
      } else {
        if (draft.items.length === 0) return t("donorX.validation.atLeastOneItem");
        const estimateText = draft.estimatedValue.trim();
        if (estimateText) {
          const estimate = Number(estimateText);
          if (!Number.isFinite(estimate) || estimate < 0) {
            return t("donorX.validation.invalidEstimatedValue");
          }
        }
      }
      return null;
    }

    if (step === 2) {
      if (
        (draft.lat === null) !== (draft.lng === null) ||
        ((draft.lat !== null || draft.lng !== null) && !hasValidPoint(draft.lat, draft.lng))
      ) {
        return t("donorX.validation.locationInvalid");
      }
      return null;
    }

    return null;
  }

  function buildInput(): RecordDonationInput {
    const input: RecordDonationInput = {
      donationType: draft.type as Choice,
      currency: draft.currency,
      idempotencyKey: draft.idempotencyKey,
      notes: draft.notes || undefined
    };

    if (selectedCampaign && selectedCampaign.currency.toUpperCase() === draft.currency) {
      input.campaignId = selectedCampaign.id;
    }

    if (draft.type === "Monetary") {
      input.amount = Number(draft.amount);
    } else {
      const items: DonationItemInput[] = draft.items.map((item) => ({
        name: item.name,
        category: item.category === "Other" ? item.customCategory : item.category,
        quantity: Number(item.quantity),
        unit: item.unit === "Other" ? item.customUnit : item.unit,
        condition: item.condition || undefined,
        notes: item.notes || undefined,
        expiryDate: item.expiryDate || undefined,
        packaging: item.packaging || undefined
      }));
      input.items = items;
      input.itemDescription = items.map((item) => item.name).join("; ").slice(0, 500);
      input.quantity = items.reduce((total, item) => total + item.quantity, 0);
      input.category = items.length === 1 ? items[0].category : undefined;
      input.unit = items.length === 1 ? items[0].unit : undefined;
      if (draft.estimatedValue.trim()) input.estimatedValue = Number(draft.estimatedValue);
    }

    if (hasValidPoint(draft.lat, draft.lng)) {
      input.latitude = draft.lat as number;
      input.longitude = draft.lng as number;
      input.locationText = (draft.locationAddress || draft.locationLabel || t("donorX.shared.locationOnMap")).slice(0, 300);
    }
    if (draft.deliveryMethod && draft.type === "Material") {
      input.deliveryMethod = draft.deliveryMethod;
    }
    return input;
  }

  async function submitDonation() {
    if (!draft.type) {
      changeStep(0);
      setFormError(t("donorX.validation.selectDonationType"));
      return;
    }
    for (let index = 0; index < 3; index += 1) {
      const error = validateStep(index);
      if (error) {
        changeStep(index);
        setFormError(error);
        return;
      }
    }

    setSubmitting(true);
    setFormError(null);
    try {
      const result = await recordDonation(buildInput());
      setSaved(result);
      try {
        window.sessionStorage.removeItem(draftKey);
      } catch {
        // Ignore storage failures after a successful recording.
      }
    } catch (cause) {
      setFormError(localizeDonationError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  function startAgain() {
    const next = blankDraft();
    setDraft(next);
    setSaved(null);
    setItemForm(blankItem());
    setEditingIndex(null);
    setFormError(null);
    setItemError(null);
    try {
      window.sessionStorage.removeItem(draftKey);
    } catch {
      // Ignore storage failures.
    }
  }

  return (
    <DonorPage
      eyebrowKey="donorX.donate.eyebrow"
      titleKey="donorX.donate.title"
      descriptionKey="donorX.donate.description"
    >
      {saved ? (
        <section className="donor-success" aria-labelledby="donor-success-heading" role="status">
          <span className="donor-success__icon" aria-hidden="true">
            <Icon name="check" size={28} />
          </span>
          <h2 id="donor-success-heading">{t("donorX.donate.successTitle")}</h2>
          <p>{t("donorX.donate.successBody")}</p>
          <dl className="donor-detail-list">
            <div>
              <dt>{t("donorX.donate.successReference")}</dt>
              <dd>{saved.reference}</dd>
            </div>
            <div>
              <dt>{t("donorX.donate.successStatus")}</dt>
              <dd>
                <span className={`donor-badge donor-badge--${donationStatusTone(saved.status)}`}>
                  {donationStatusLabel(t, saved.status)}
                </span>
              </dd>
            </div>
            <div>
              <dt>{t("donorX.donate.successSummary")}</dt>
              <dd>{donationSummary(t, saved)}</dd>
            </div>
          </dl>
          <PortalNotice tone="success">{t("donorX.shared.staffOnly")}</PortalNotice>
          <h3>{t("donorX.donate.successNext")}</h3>
          <p>{t("donorX.donate.successNextBody")}</p>
          <div className="donor-success__actions">
            <Link className="btn btn-primary" href={`/donor/donations/${encodeURIComponent(saved.id)}`}>
              {t("donorX.donate.trackDonation")} <Icon name="arrow" size={16} />
            </Link>
            <Link className="btn btn-outline" href="/portal/donor">
              {t("donorX.donate.backPortal")} <Icon name="arrow" size={16} />
            </Link>
            <button className="btn btn-outline" type="button" onClick={startAgain}>
              {t("donorX.donate.startAnother")}
            </button>
          </div>
        </section>
      ) : !ready ? (
        <PortalState kind="loading" title={t("donorX.donate.loadingTitle")}>
          {t("donorX.donate.loadingBody")}
        </PortalState>
      ) : (
        <>
          <nav className="donor-stepper-nav" aria-label={t("donorX.donate.title")}>
            <ol className="donor-stepper">
              {STEP_KEYS.map((key, index) => {
                const state = index < draft.step ? "done" : index === draft.step ? "current" : "upcoming";
                return (
                  <li className={`donor-stepper__item is-${state}`} key={key}>
                    <button
                      type="button"
                      className="donor-stepper__button"
                      disabled={index > draft.step || submitting}
                      aria-current={index === draft.step ? "step" : undefined}
                      onClick={() => changeStep(index)}
                    >
                      <span className="donor-stepper__marker" aria-hidden="true">
                        {state === "done" ? <Icon name="check" size={15} /> : index + 1}
                      </span>
                      <span className="donor-stepper__label">{t(key)}</span>
                    </button>
                    {index < STEP_KEYS.length - 1 ? (
                      <span className="donor-stepper__connector" aria-hidden="true" />
                    ) : null}
                  </li>
                );
              })}
            </ol>
          </nav>

          {formError ? <PortalNotice tone="error">{formError}</PortalNotice> : null}

          <section className="donor-wizard-card" aria-labelledby="donor-step-heading">
            {draft.step === 0 ? (
              <>
                <header className="donor-step-head">
                  <h2 id="donor-step-heading">{t("donorX.donate.typeTitle")}</h2>
                  <p>{t("donorX.donate.typeDescription")}</p>
                </header>
                <div className="donor-choice-grid">
                  <button
                    type="button"
                    className={`donor-choice${draft.type === "Monetary" ? " is-selected" : ""}`}
                    aria-pressed={draft.type === "Monetary"}
                    onClick={() => updateDraft("type", "Monetary")}
                  >
                    <span className="donor-choice__icon donor-choice__icon--green" aria-hidden="true">
                      <Icon name="heart" size={24} />
                    </span>
                    <span className="donor-choice__body">
                      <b>{t("donorX.donate.financialTitle")}</b>
                      <span>{t("donorX.donate.financialCopy")}</span>
                    </span>
                    {draft.type === "Monetary" ? (
                      <span className="donor-choice__selected">
                        <Icon name="check" size={15} /> {t("donorX.donate.selected")}
                      </span>
                    ) : null}
                  </button>
                  <button
                    type="button"
                    className={`donor-choice${draft.type === "Material" ? " is-selected" : ""}`}
                    aria-pressed={draft.type === "Material"}
                    onClick={() => updateDraft("type", "Material")}
                  >
                    <span className="donor-choice__icon donor-choice__icon--blue" aria-hidden="true">
                      <Icon name="box" size={24} />
                    </span>
                    <span className="donor-choice__body">
                      <b>{t("donorX.donate.suppliesTitle")}</b>
                      <span>{t("donorX.donate.suppliesCopy")}</span>
                    </span>
                    {draft.type === "Material" ? (
                      <span className="donor-choice__selected">
                        <Icon name="check" size={15} /> {t("donorX.donate.selected")}
                      </span>
                    ) : null}
                  </button>
                </div>
              </>
            ) : null}

            {draft.step === 1 ? (
              <>
                <header className="donor-step-head">
                  <h2 id="donor-step-heading">{t("donorX.donate.detailsTitle")}</h2>
                </header>

                {draft.type === "Monetary" ? (
                  <div className="donor-form-grid">
                    <div className="donor-field">
                      <label htmlFor="donor-currency">{t("donorX.donate.currencyLabel")}</label>
                      <select
                        id="donor-currency"
                        value={draft.currency}
                        onChange={(event) => changeCurrency(event.target.value as Currency)}
                      >
                        {SUPPORTED_CURRENCIES.map((currency) => (
                          <option value={currency} key={currency}>{currency}</option>
                        ))}
                      </select>
                    </div>

                    <div className="donor-field donor-field--wide">
                      <span className="donor-field__label" id="donor-suggestions-label">
                        {t("donorX.donate.suggestedAmounts")}
                      </span>
                      <div className="donor-amount-options" aria-labelledby="donor-suggestions-label">
                        {SUGGESTED_AMOUNTS[draft.currency].map((amount) => (
                          <button
                            type="button"
                            key={amount}
                            className={`donor-amount-chip${draft.amount === String(amount) ? " is-selected" : ""}`}
                            aria-pressed={draft.amount === String(amount)}
                            onClick={() => updateDraft("amount", String(amount))}
                          >
                            {formatMoney(amount, draft.currency)}
                          </button>
                        ))}
                      </div>
                    </div>

                    <div className="donor-field donor-field--wide">
                      <label htmlFor="donor-amount">
                        {t("donorX.donate.amountLabel", { currency: draft.currency })}
                      </label>
                      <span className="donor-field__hint">{t("donorX.donate.customAmount")}</span>
                      <input
                        id="donor-amount"
                        type="number"
                        min="0.01"
                        step="0.01"
                        inputMode="decimal"
                        value={draft.amount}
                        placeholder={t("donorX.donate.amountPlaceholder")}
                        onChange={(event) => updateDraft("amount", event.target.value)}
                      />
                    </div>

                    <div className="donor-field donor-field--wide">
                      <label htmlFor="donor-campaign">{t("donorX.donate.campaignLabel")}</label>
                      <select
                        id="donor-campaign"
                        value={draft.campaignId}
                        onChange={(event) => changeCampaign(event.target.value)}
                        disabled={campaignState === "loading"}
                      >
                        <option value="">{t("donorX.donate.campaignNone")}</option>
                        {visibleCampaigns.map((campaign) => (
                          <option value={campaign.id} key={campaign.id}>
                            {campaign.name} · {campaign.currency}
                          </option>
                        ))}
                      </select>
                      {campaignState === "loading" ? (
                        <span className="donor-field__hint">{t("donorX.needs.loadingBody")}</span>
                      ) : null}
                      {campaignState === "error" ? (
                        <span className="donor-field__error" role="alert">{campaignError}</span>
                      ) : null}
                      {selectedCampaign && selectedCampaign.currency.toUpperCase() !== draft.currency ? (
                        <span className="donor-field__error" role="alert">{t("donorX.donate.campaignCurrencyMismatch")}</span>
                      ) : null}
                    </div>

                    <div className="donor-field donor-field--wide">
                      <label htmlFor="donor-notes">{t("donorX.donate.notesLabel")}</label>
                      <textarea
                        id="donor-notes"
                        rows={3}
                        maxLength={1000}
                        value={draft.notes}
                        placeholder={t("donorX.donate.notesPlaceholder")}
                        onChange={(event) => updateDraft("notes", event.target.value)}
                      />
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="donor-form-grid">
                      <div className="donor-field">
                        <label htmlFor="donor-material-currency">{t("donorX.donate.currencyLabel")}</label>
                        <select
                          id="donor-material-currency"
                          value={draft.currency}
                          onChange={(event) => changeCurrency(event.target.value as Currency)}
                        >
                          {SUPPORTED_CURRENCIES.map((currency) => (
                            <option value={currency} key={currency}>{currency}</option>
                          ))}
                        </select>
                      </div>
                      <div className="donor-field">
                        <label htmlFor="donor-estimated">
                          {t("donorX.donate.estimatedLabel", { currency: draft.currency })}
                        </label>
                        <input
                          id="donor-estimated"
                          type="number"
                          min="0"
                          step="0.01"
                          inputMode="decimal"
                          value={draft.estimatedValue}
                          placeholder={t("donorX.donate.estimatedPlaceholder")}
                          onChange={(event) => updateDraft("estimatedValue", event.target.value)}
                        />
                      </div>
                      <div className="donor-field donor-field--wide">
                        <label htmlFor="donor-material-campaign">{t("donorX.donate.campaignLabel")}</label>
                        <select
                          id="donor-material-campaign"
                          value={draft.campaignId}
                          onChange={(event) => changeCampaign(event.target.value)}
                          disabled={campaignState === "loading"}
                        >
                          <option value="">{t("donorX.donate.campaignNone")}</option>
                          {visibleCampaigns.map((campaign) => (
                            <option value={campaign.id} key={campaign.id}>
                              {campaign.name} · {campaign.currency}
                            </option>
                          ))}
                        </select>
                        {campaignState === "loading" ? (
                          <span className="donor-field__hint">{t("donorX.needs.loadingBody")}</span>
                        ) : null}
                        {campaignState === "error" ? (
                          <span className="donor-field__error" role="alert">{campaignError}</span>
                        ) : null}
                        {selectedCampaign && selectedCampaign.currency.toUpperCase() !== draft.currency ? (
                          <span className="donor-field__error" role="alert">{t("donorX.donate.campaignCurrencyMismatch")}</span>
                        ) : null}
                      </div>
                    </div>

                    <div className="donor-item-builder">
                      <h3>{t("donorX.donate.itemsTitle")}</h3>
                      <p>{t("donorX.donate.itemsDescription")}</p>
                      {itemError ? <PortalNotice tone="error">{itemError}</PortalNotice> : null}
                      <div className="donor-form-grid">
                        <div className="donor-field donor-field--wide">
                          <label htmlFor="donor-item-name">{t("donorX.donate.itemNameLabel")}</label>
                          <input
                            id="donor-item-name"
                            maxLength={200}
                            value={itemForm.name}
                            placeholder={t("donorX.donate.itemNamePlaceholder")}
                            onChange={(event) => changeItemField("name", event.target.value)}
                          />
                        </div>
                        <div className="donor-field">
                          <label htmlFor="donor-item-category">{t("donorX.donate.categoryLabel")}</label>
                          <select
                            id="donor-item-category"
                            value={itemForm.category}
                            onChange={(event) => changeItemField("category", event.target.value)}
                          >
                            <option value="">—</option>
                            {DONOR_CATEGORIES.map((option) => (
                              <option value={option.value} key={option.value}>{t(option.labelKey)}</option>
                            ))}
                          </select>
                        </div>
                        {itemForm.category === "Other" ? (
                          <div className="donor-field">
                            <label htmlFor="donor-item-custom-category">
                              {t("donorX.donate.customCategoryLabel")}
                            </label>
                            <input
                              id="donor-item-custom-category"
                              maxLength={80}
                              value={itemForm.customCategory}
                              placeholder={t("donorX.donate.customCategoryPlaceholder")}
                              onChange={(event) => changeItemField("customCategory", event.target.value)}
                            />
                          </div>
                        ) : null}
                        <div className="donor-field">
                          <label htmlFor="donor-item-quantity">{t("donorX.donate.quantityLabel")}</label>
                          <input
                            id="donor-item-quantity"
                            type="number"
                            min="1"
                            step="1"
                            inputMode="numeric"
                            value={itemForm.quantity}
                            onChange={(event) => changeItemField("quantity", event.target.value)}
                          />
                        </div>
                        <div className="donor-field">
                          <label htmlFor="donor-item-unit">{t("donorX.donate.unitLabel")}</label>
                          <select
                            id="donor-item-unit"
                            value={itemForm.unit}
                            onChange={(event) => changeItemField("unit", event.target.value)}
                          >
                            <option value="">—</option>
                            {DONOR_UNITS.map((option) => (
                              <option value={option.value} key={option.value}>{t(option.labelKey)}</option>
                            ))}
                          </select>
                        </div>
                        {itemForm.unit === "Other" ? (
                          <div className="donor-field">
                            <label htmlFor="donor-item-custom-unit">{t("donorX.donate.customUnitLabel")}</label>
                            <input
                              id="donor-item-custom-unit"
                              maxLength={80}
                              value={itemForm.customUnit}
                              placeholder={t("donorX.donate.customUnitPlaceholder")}
                              onChange={(event) => changeItemField("customUnit", event.target.value)}
                            />
                          </div>
                        ) : null}
                        <div className="donor-field donor-field--wide">
                          <label htmlFor="donor-item-condition">{t("donorX.donate.conditionLabel")}</label>
                          <input
                            id="donor-item-condition"
                            maxLength={100}
                            value={itemForm.condition}
                            placeholder={t("donorX.donate.conditionPlaceholder")}
                            onChange={(event) => changeItemField("condition", event.target.value)}
                          />
                        </div>
                        <div className="donor-field">
                          <label htmlFor="donor-item-expiry">{t("donorX.donate.expiryLabel")}</label>
                          <input
                            id="donor-item-expiry"
                            type="date"
                            min={utcDateInputValue()}
                            value={itemForm.expiryDate}
                            onChange={(event) => changeItemField("expiryDate", event.target.value)}
                          />
                        </div>
                        <div className="donor-field">
                          <label htmlFor="donor-item-packaging">{t("donorX.donate.packagingLabel")}</label>
                          <input
                            id="donor-item-packaging"
                            maxLength={200}
                            value={itemForm.packaging}
                            placeholder={t("donorX.donate.packagingPlaceholder")}
                            onChange={(event) => changeItemField("packaging", event.target.value)}
                          />
                        </div>
                        <div className="donor-field donor-field--wide">
                          <label htmlFor="donor-item-notes">{t("donorX.donate.itemNotesLabel")}</label>
                          <textarea
                            id="donor-item-notes"
                            rows={2}
                            maxLength={500}
                            value={itemForm.notes}
                            placeholder={t("donorX.donate.itemNotesPlaceholder")}
                            onChange={(event) => changeItemField("notes", event.target.value)}
                          />
                        </div>
                      </div>
                      <div className="donor-item-builder__actions">
                        <button className="btn btn-primary" type="button" onClick={commitItem}>
                          <Icon name={editingIndex === null ? "plus" : "check"} size={16} />
                          {editingIndex === null
                            ? t("donorX.donate.addItem")
                            : t("donorX.donate.saveItem")}
                        </button>
                        {editingIndex !== null ? (
                          <button
                            className="btn btn-outline"
                            type="button"
                            onClick={() => {
                              setItemForm(blankItem());
                              setEditingIndex(null);
                              setItemError(null);
                            }}
                          >
                            {t("donorX.donate.cancelEdit")}
                          </button>
                        ) : null}
                      </div>

                      <h4>{t("donorX.donate.itemSummary")}</h4>
                      {draft.items.length === 0 ? (
                        <p className="donor-muted">{t("donorX.donate.noItems")}</p>
                      ) : (
                        <ul className="donor-item-summary">
                          {draft.items.map((item, index) => {
                            const itemCategory =
                              item.category === "Other" ? item.customCategory : item.category;
                            const itemUnit = item.unit === "Other" ? item.customUnit : item.unit;
                            return (
                              <li key={item.key}>
                                <span className="donor-item-summary__icon" aria-hidden="true">
                                  <Icon name={categoryIcon(item.category)} size={18} />
                                </span>
                                <span className="donor-item-summary__body">
                                  <b>{item.name}</b>
                                  <small>
                                    {item.quantity} {unitLabel(t, itemUnit)} · {categoryLabel(t, itemCategory)}
                                  </small>
                                </span>
                                <button
                                  className="donor-icon-button"
                                  type="button"
                                  aria-label={`${t("donorX.donate.editItem")}: ${item.name}`}
                                  onClick={() => editItem(index)}
                                >
                                  <Icon name="file" size={16} />
                                </button>
                                <button
                                  className="donor-icon-button donor-icon-button--danger"
                                  type="button"
                                  aria-label={`${t("donorX.donate.removeItem")}: ${item.name}`}
                                  onClick={() => removeItem(index)}
                                >
                                  <Icon name="close" size={16} />
                                </button>
                              </li>
                            );
                          })}
                        </ul>
                      )}

                      <aside className="donor-guidance" aria-labelledby="donor-guidance-heading">
                        <h4 id="donor-guidance-heading">{t("donorX.donate.guidanceTitle")}</h4>
                        <ul>
                          <li>{t("donorX.donate.guidanceOne")}</li>
                          <li>{t("donorX.donate.guidanceTwo")}</li>
                          <li>{t("donorX.donate.guidanceThree")}</li>
                        </ul>
                      </aside>
                    </div>
                  </>
                )}
                <PortalNotice>{t("donorX.shared.noPayment")}</PortalNotice>
              </>
            ) : null}

            {draft.step === 2 ? (
              <>
                <header className="donor-step-head">
                  <h2 id="donor-step-heading">{t("donorX.donate.locationTitle")}</h2>
                  <p>{t("donorX.donate.locationDescription")}</p>
                </header>
                {draft.type === "Monetary" ? (
                  <PortalNotice>{t("donorX.donate.locationOptional")}</PortalNotice>
                ) : null}
                <DonationLocationPicker
                  lat={draft.lat}
                  lng={draft.lng}
                  label={draft.locationLabel}
                  address={draft.locationAddress}
                  onChange={changeLocation}
                />
                {draft.type === "Material" ? (
                  <div className="donor-delivery">
                    <div className="donor-field donor-field--wide">
                      <label htmlFor="donor-delivery">{t("donorX.donate.deliveryLabel")}</label>
                      <select
                        id="donor-delivery"
                        value={draft.deliveryMethod}
                        onChange={(event) => updateDraft("deliveryMethod", event.target.value)}
                      >
                        <option value="">—</option>
                        {DELIVERY_METHODS.map((method) => (
                          <option value={method} key={method}>{deliveryLabel(t, method)}</option>
                        ))}
                      </select>
                    </div>
                    {draft.deliveryMethod === "Request collection" ? (
                      <PortalNotice tone="error">{t("donorX.donate.pickupNote")}</PortalNotice>
                    ) : null}
                    <aside className="donor-guidance" aria-labelledby="donor-centres-heading">
                      <h4 id="donor-centres-heading">{t("donorX.donate.centresTitle")}</h4>
                      <p>{t("donorX.donate.centresUnavailable")}</p>
                    </aside>
                  </div>
                ) : null}
              </>
            ) : null}

            {draft.step === 3 ? (
              <>
                <header className="donor-step-head">
                  <h2 id="donor-step-heading">{t("donorX.donate.reviewTitle")}</h2>
                  <p>{t("donorX.donate.reviewDescription")}</p>
                </header>
                <dl className="donor-review-list">
                  <div>
                    <dt>{t("donorX.donate.reviewType")}</dt>
                    <dd>
                      {draft.type === "Monetary"
                        ? t("donorX.donate.reviewFinancial")
                        : t("donorX.donate.reviewSupplies")}
                    </dd>
                    <button type="button" onClick={() => changeStep(0)}>
                      {t("donorX.donate.editStep")} <Icon name="arrow" size={14} />
                    </button>
                  </div>
                  {draft.type === "Monetary" ? (
                    <div>
                      <dt>{t("donorX.donate.reviewAmount")}</dt>
                      <dd>{formatMoney(Number(draft.amount), draft.currency)}</dd>
                      <button type="button" onClick={() => changeStep(1)}>
                        {t("donorX.donate.editStep")} <Icon name="arrow" size={14} />
                      </button>
                    </div>
                  ) : (
                    <div>
                      <dt>{t("donorX.donate.reviewItems")}</dt>
                      <dd>
                        <ul className="donor-review-items">
                          {draft.items.map((item) => (
                            <li key={item.key}>
                              {item.quantity}{" "}
                              {unitLabel(t, item.unit === "Other" ? item.customUnit : item.unit)}{" "}
                              {item.name} ·{" "}
                              {categoryLabel(t, item.category === "Other" ? item.customCategory : item.category)}
                            </li>
                          ))}
                        </ul>
                      </dd>
                      <button type="button" onClick={() => changeStep(1)}>
                        {t("donorX.donate.editStep")} <Icon name="arrow" size={14} />
                      </button>
                    </div>
                  )}
                  <div>
                    <dt>{t("donorX.donate.reviewCampaign")}</dt>
                    <dd>
                      {selectedCampaign?.name ?? t("donorX.detail.noCampaign")}
                    </dd>
                    <button type="button" onClick={() => changeStep(1)}>
                      {t("donorX.donate.editStep")} <Icon name="arrow" size={14} />
                    </button>
                  </div>
                  <div>
                    <dt>{t("donorX.donate.reviewLocation")}</dt>
                    <dd>
                      {hasValidPoint(draft.lat, draft.lng)
                        ? draft.locationLabel || t("donorX.shared.locationOnMap")
                        : t("donorX.shared.locationNotSpecified")}
                    </dd>
                    <button type="button" onClick={() => changeStep(2)}>
                      {t("donorX.donate.editStep")} <Icon name="arrow" size={14} />
                    </button>
                  </div>
                  {draft.type === "Material" ? (
                    <div>
                      <dt>{t("donorX.donate.reviewDelivery")}</dt>
                      <dd>{deliveryLabel(t, draft.deliveryMethod)}</dd>
                    </div>
                  ) : null}
                  {draft.notes ? (
                    <div>
                      <dt>{t("donorX.donate.reviewNotes")}</dt>
                      <dd>{draft.notes}</dd>
                    </div>
                  ) : null}
                </dl>
                <aside className="donor-guidance" aria-labelledby="donor-meaning-heading">
                  <h4 id="donor-meaning-heading">{t("donorX.donate.meaningTitle")}</h4>
                  <p>{t("donorX.donate.meaningBody")}</p>
                </aside>
              </>
            ) : null}
          </section>

          <div className="donor-wizard-actions">
            {draft.step > 0 ? (
              <button
                className="btn btn-outline"
                type="button"
                onClick={() => changeStep(draft.step - 1)}
                disabled={submitting}
              >
                <Icon name="arrowLeft" size={16} /> {t("donorX.shared.back")}
              </button>
            ) : (
              <Link className="btn btn-outline" href="/portal/donor">
                <Icon name="arrowLeft" size={16} /> {t("donorX.shared.backPortal")}
              </Link>
            )}
            {draft.step < 3 ? (
              <button
                className="btn btn-primary"
                type="button"
                onClick={() => {
                  const error = validateStep(draft.step);
                  if (error) setFormError(error);
                  else changeStep(draft.step + 1);
                }}
              >
                {t("donorX.shared.continue")} <Icon name="arrow" size={16} />
              </button>
            ) : (
              <button
                className="btn btn-primary"
                type="button"
                onClick={() => void submitDonation()}
                disabled={submitting}
                aria-busy={submitting}
              >
                {submitting ? t("donorX.shared.submitting") : t("donorX.shared.submit")}
                {!submitting ? <Icon name="check" size={16} /> : null}
              </button>
            )}
          </div>
        </>
      )}
    </DonorPage>
  );
}
