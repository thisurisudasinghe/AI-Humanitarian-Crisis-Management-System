/**
 * Single editable source for public contact information.
 *
 * No verified public phone number, email address, or office address exists in
 * the repository yet. Keep `verified` false and `value` null until a real
 * detail has been confirmed. The placeholders below are shown explicitly in
 * every locale so they are never mistaken for real contact details.
 */
export type PublicContactChannel = {
  id: "email" | "phone" | "address";
  icon: "mail" | "phone" | "pin";
  labelKey: string;
  value: string | null;
  placeholderKey: string;
};

export const publicContact: {
  verified: boolean;
  channels: PublicContactChannel[];
} = {
  verified: false,
  channels: [
    {
      id: "email",
      icon: "mail",
      labelKey: "info.contact.detailEmail",
      value: null,
      placeholderKey: "info.contact.placeholderEmail"
    },
    {
      id: "phone",
      icon: "phone",
      labelKey: "info.contact.detailPhone",
      value: null,
      placeholderKey: "info.contact.placeholderPhone"
    },
    {
      id: "address",
      icon: "pin",
      labelKey: "info.contact.detailAddress",
      value: null,
      placeholderKey: "info.contact.placeholderAddress"
    }
  ]
};
