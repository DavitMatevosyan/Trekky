import { Component, type ReactNode } from 'react'

/** Shows `fallback` instead of a blank page when its children throw while rendering (e.g. a lazy chunk fails to load). */
export class ErrorBoundary extends Component<{ fallback: ReactNode; children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  render() {
    return this.state.failed ? this.props.fallback : this.props.children
  }
}
