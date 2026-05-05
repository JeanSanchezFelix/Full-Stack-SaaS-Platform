import CustomerIntakeEnhancer from './lib/components/CustomerIntakeEnhancer.svelte';
import { mount } from 'svelte';

export function mountCustomerIntakeEnhancer(target, options = {}) {
  return mount(CustomerIntakeEnhancer, {
    target,
    props: options.props || {}
  });
}
