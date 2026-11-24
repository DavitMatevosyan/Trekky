import { useParams } from 'react-router'
import { DocsRepositorySection } from '../github/DocsRepositorySection.tsx'
import { ProjectHeader } from '../projects/ProjectHeader.tsx'
import { ProjectLoader } from '../projects/ProjectLoader.tsx'

/** A project's settings (TREK-74): where its docs live on GitHub. */
export function ProjectSettingsPage() {
  const { projectKey = '' } = useParams()
  return (
    <ProjectLoader projectKey={projectKey}>
      {(project) => (
        <section className="grid gap-6">
          <ProjectHeader projectKey={project.key} projectName={project.name} />
          <DocsRepositorySection key={project.id} projectId={project.id} />
        </section>
      )}
    </ProjectLoader>
  )
}
