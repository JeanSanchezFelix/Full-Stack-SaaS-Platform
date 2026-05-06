import UserInfoDisplay from './lib/components/UserInfoDisplay.svelte';
import { mount } from 'svelte';

export function mountUserInfoDisplay(target, options = {}) {
  return mount(UserInfoDisplay, {
    target,
    props: options.props || {}
  });
}
