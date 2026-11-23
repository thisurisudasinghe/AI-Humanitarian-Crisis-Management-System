/**
 * Single source of truth for SAFEWAY public contact information.
 * Labels stay in the locale dictionaries; contact values and links live here.
 */
export type PublicContactChannel = {
  id: "email" | "phone";
  icon: "mail" | "phone";
  labelKey: string;
  value: string;
  displayValue: string;
  href: string;
};

export const publicContact: {
  verified: boolean;
  channels: PublicContactChannel[];
} = {
  verified: true,
  channels: [
    {
      id: "email",
      icon: "mail",
      labelKey: "info.contact.detailEmail",
      value: "safeway@gmail.com",
      displayValue: "safeway@gmail.com",
      href: "mailto:safeway@gmail.com"
    },
    {
      id: "phone",
      icon: "phone",
      labelKey: "info.contact.detailPhone",
      value: "0112191919",
      displayValue: "011 219 1919",
      href: "tel:0112191919"
    }
  ]
};
