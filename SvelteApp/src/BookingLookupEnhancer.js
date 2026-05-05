import BookingLookupEnhancer from './lib/components/BookingLookupEnhancer.svelte';
import { mount } from 'svelte';

export function mountBookingLookupEnhancer(target, options = {}) {
  return mount(BookingLookupEnhancer, {
    target,
    props: options.props || {}
  });
}
