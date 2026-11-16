import { Slot } from '@radix-ui/react-slot'
import type { VariantProps } from 'class-variance-authority'
import type { ComponentProps } from 'react'
import { cn } from '../../lib/cn.ts'
import { buttonVariants } from './buttonVariants.ts'

export interface ButtonProps extends ComponentProps<'button'>, VariantProps<typeof buttonVariants> {
  /** Render the child element (e.g. a router Link) with button styles. */
  asChild?: boolean
}

export function Button({ className, variant, size, asChild = false, ...props }: ButtonProps) {
  const Component = asChild ? Slot : 'button'
  return <Component className={cn(buttonVariants({ variant, size }), className)} {...props} />
}
