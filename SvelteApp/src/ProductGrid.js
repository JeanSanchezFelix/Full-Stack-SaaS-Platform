import { mount as svelteMount } from 'svelte';
import ProductGrid from './lib/components/ProductGrid.svelte';

export function mount(target, options = {}) {
  target.replaceChildren();

  return svelteMount(ProductGrid, {
    target,
    props: options.props ?? {}
  });
}
