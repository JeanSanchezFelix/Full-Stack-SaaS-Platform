import ActionButton from "./lib/components/ActionButton.svelte";
import { mount } from "svelte";

export function mountActionButton(target, options = {}) {
  return mount(ActionButton, {
    target,
    props: options.props || {},
  });
}

