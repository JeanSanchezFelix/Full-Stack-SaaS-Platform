import { mountCustomerIntakeEnhancer } from './CustomerIntakeEnhancer.js';
import { mountBookingLookupEnhancer } from './BookingLookupEnhancer.js';
import { mountUserInfoDisplay } from './UserInfoDisplay.js';
import { mountTextEntryField } from './TextEntryField.js';
import { mountActionButton } from './ActionButton.js';

const componentMounts = {
  CustomerIntakeEnhancer: mountCustomerIntakeEnhancer,
  BookingLookupEnhancer: mountBookingLookupEnhancer,
  UserInfoDisplay: mountUserInfoDisplay,
  TextEntryField: mountTextEntryField,
  ActionButton: mountActionButton
};

function parseProps(element) {
  try {
    return JSON.parse(element.dataset.props || '{}');
  } catch {
    return {};
  }
}

document.querySelectorAll('[data-svelte-component]').forEach((element) => {
  const componentName = element.dataset.svelteComponent;
  const mount = componentMounts[componentName];

  if (!mount || element.dataset.svelteMounted === 'true') {
    return;
  }

  mount(element, { props: parseProps(element) });
  element.dataset.svelteMounted = 'true';
});
