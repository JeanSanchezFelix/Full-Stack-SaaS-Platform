import TextEntryField from './lib/components/TextEntryField.svelte';
import { mount } from 'svelte';

export function mountTextEntryField(target, options = {}) {
  return mount(TextEntryField, {
    target,
    props: options.props || {}
  });
}
