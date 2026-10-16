import { useMemo, useState } from 'react'

type View = 'main' | 'settings' | 'guide'
type PreviewState = 'default' | 'hover' | 'error' | 'empty'
type IconName =
  | 'arrow'
  | 'check'
  | 'chevron'
  | 'close'
  | 'copy'
  | 'delete'
  | 'external'
  | 'file'
  | 'folder'
  | 'link'
  | 'moon'
  | 'more'
  | 'play'
  | 'search'
  | 'settings'
  | 'spark'
  | 'sun'

const iconPaths: Record<IconName, React.ReactNode> = {
  arrow: <><path d="M5 12h14M13 6l6 6-6 6" /></>,
  check: <path d="m5 12 4 4L19 6" />,
  chevron: <path d="m9 18 6-6-6-6" />,
  close: <><path d="m7 7 10 10M17 7 7 17" /></>,
  copy: <><rect x="8" y="8" width="11" height="11" rx="2" /><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2" /></>,
  delete: <><path d="M4 7h16M9 7V4h6v3M7 7l1 13h8l1-13M10 11v5M14 11v5" /></>,
  external: <><path d="M14 5h5v5M19 5l-8 8" /><path d="M17 13v5a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1h5" /></>,
  file: <><path d="M7 3h7l4 4v14H7z" /><path d="M14 3v5h5" /></>,
  folder: <path d="M3 7.5h7l2-2h9v13H3z" />,
  link: <><path d="M10 14 8.5 15.5a3.5 3.5 0 0 1-5-5L7 7a3.5 3.5 0 0 1 5 0" /><path d="m14 10 1.5-1.5a3.5 3.5 0 0 1 5 5L17 17a3.5 3.5 0 0 1-5 0M8 12h8" /></>,
  moon: <path d="M19 15.5A8 8 0 0 1 8.5 5 8 8 0 1 0 19 15.5Z" />,
  more: <><circle cx="5" cy="12" r="1" fill="currentColor" stroke="none" /><circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" /><circle cx="19" cy="12" r="1" fill="currentColor" stroke="none" /></>,
  play: <path d="m8 5 11 7-11 7z" />,
  search: <><circle cx="11" cy="11" r="7" /><path d="m16 16 4 4" /></>,
  settings: <><circle cx="12" cy="12" r="3" /><path d="M19 13.5v-3l-2-.7a7 7 0 0 0-.8-1.8l.9-2-2.1-2.1-2 .9a7 7 0 0 0-1.8-.8l-.7-2h-3l-.7 2a7 7 0 0 0-1.8.8l-2-.9L.9 6l.9 2A7 7 0 0 0 1 9.8l-2 .7v3l2 .7a7 7 0 0 0 .8 1.8l-.9 2L3 20.1l2-.9a7 7 0 0 0 1.8.8l.7 2h3l.7-2a7 7 0 0 0 1.8-.8l2 .9 2.1-2.1-.9-2a7 7 0 0 0 .8-1.8z" transform="translate(2.5 0) scale(.8)" /></>,
  spark: <><path d="m12 3 1.2 4.1L17 9l-3.8 1.9L12 15l-1.2-4.1L7 9l3.8-1.9z" /><path d="m18.5 15 .6 2.1L21 18l-1.9.9-.6 2.1-.6-2.1L16 18l1.9-.9z" /></>,
  sun: <><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" /></>,
}

function Icon({ name, size = 18 }: { name: IconName; size?: number }) {
  return <svg className="icon" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{iconPaths[name]}</svg>
}

function TitleBar({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div className="titlebar">
      <div className="brand-mark"><Icon name="spark" size={16} /></div>
      <div className="window-titles"><strong>{title}</strong>{subtitle && <span>{subtitle}</span>}</div>
      <div className="window-controls" aria-label="Window controls">
        <button aria-label="Minimize">—</button>
        <button aria-label="Maximize">□</button>
        <button className="window-close" aria-label="Close">×</button>
      </div>
    </div>
  )
}

function MainWindow({ state }: { state: PreviewState }) {
  const [destination, setDestination] = useState(state === 'error' ? 'https://logs.example.com/vicki' : '')
  const [name, setName] = useState(state === 'error' ? 'l-vicki' : '')
  const [busy, setBusy] = useState(false)
  const [managerOpen, setManagerOpen] = useState(false)
  const [browseMenuOpen, setBrowseMenuOpen] = useState(false)
  const isError = state === 'error'
  const destinationIcon = destination.startsWith('http') ? 'link' : destination ? 'folder' : 'arrow'

  const create = () => {
    if (!destination || !name) return
    setBusy(true)
    window.setTimeout(() => {
      setBusy(false)
      setDestination('')
      setName('')
    }, 900)
  }

  return (
    <div className={`utility-window keycuts-shell ${managerOpen ? 'manager-open' : ''}`}>
      <TitleBar title="keycuts" subtitle={managerOpen ? 'Shortcut manager' : 'New shortcut'} />
      {managerOpen ? (
        <BatmanagerPanel state={state} onClose={() => setManagerOpen(false)} />
      ) : (
        <main className="window-body main-window">
          <div className="intro compact-intro">
            <div><span className="eyebrow">NEW KEYCUT</span><h1>Launch anything instantly</h1></div>
            <button className="icon-button" aria-label="Open settings"><Icon name="settings" /></button>
          </div>

          <div className={`quick-drop ${state === 'hover' ? 'is-hovered' : ''} ${isError ? 'has-error' : ''} ${destination ? 'has-destination' : ''}`}>
            <div className="quick-drop-icon"><Icon name={destinationIcon} size={23} /></div>
            <div className="quick-drop-copy">
              <strong>Destination</strong>
              <input
                className="quick-destination-input"
                aria-label="Destination"
                value={destination}
                onChange={(event) => setDestination(event.target.value)}
                placeholder="Drop or paste a file, folder, or URL here"
              />
            </div>
            <div className="browse-menu-wrap">
              <button
                className="browse-button"
                aria-haspopup="menu"
                aria-expanded={browseMenuOpen}
                onClick={() => setBrowseMenuOpen(!browseMenuOpen)}
              >
                Browse<Icon name="chevron" size={12} />
              </button>
              {browseMenuOpen && (
                <div className="browse-choice-menu" role="menu">
                  <button role="menuitem" onClick={() => { setDestination('C:\\Users\\Maya\\Documents\\project-brief.pdf'); setBrowseMenuOpen(false) }}>
                    <Icon name="file" size={16} /><span><strong>File…</strong><small>Choose a single file</small></span>
                  </button>
                  <button role="menuitem" onClick={() => { setDestination('C:\\...\\Downloads'); setBrowseMenuOpen(false) }}>
                    <Icon name="folder" size={16} /><span><strong>Folder…</strong><small>Choose a directory</small></span>
                  </button>
                </div>
              )}
            </div>
          </div>

          <form className="name-form" onSubmit={(event) => { event.preventDefault(); create() }}>
            <label htmlFor="shortcut-name">Name your keycut</label>
            <div className="name-row">
              <input
                id="shortcut-name"
                autoFocus={Boolean(destination)}
                className={isError ? 'input-error' : ''}
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="e.g. l-vicki"
              />
              <button className="primary-button create-button" disabled={!destination || !name || busy} type="submit">
                {busy ? <><span className="spinner" />Saving</> : <>Create<Icon name="arrow" size={16} /></>}
              </button>
            </div>
            {isError ? <p className="field-message error-text">“l-vicki” already exists. Choose another name.</p> : <p className="field-message">Press Enter to create it</p>}
          </form>

          <div className="compact-footer">
            <button className="manager-link" onClick={() => setManagerOpen(true)}>
              <Icon name="external" size={15} />
              View all keycuts
              <span>{managerRows.length}</span>
            </button>
          </div>
        </main>
      )}
    </div>
  )
}

function Toggle({ checked, onChange, label, note }: { checked: boolean; onChange: () => void; label: string; note: string }) {
  return (
    <div className="setting-row">
      <div><label>{label}</label><p>{note}</p></div>
      <button className={`toggle ${checked ? 'on' : ''}`} onClick={onChange} role="switch" aria-checked={checked} aria-label={label}><span /></button>
    </div>
  )
}

function SettingsWindow({ state }: { state: PreviewState }) {
  const [overwrite, setOverwrite] = useState(state === 'hover')
  const [contexts, setContexts] = useState(true)
  return (
    <div className="utility-window settings-window">
      <TitleBar title="Settings" subtitle="keycuts" />
      <main className="window-body">
        <div className="section-heading"><div><h1>Settings</h1><p>Choose how keycuts creates your shortcuts.</p></div></div>
        <section className="settings-section">
          <span className="section-label">OUTPUT</span>
          <div className={`input-group ${state === 'error' ? 'has-error' : ''}`}>
            <label htmlFor="output-folder">Output folder</label>
            <div className="field-row">
              <input id="output-folder" defaultValue={state === 'empty' ? '' : 'C:\\Users\\Maya\\keycuts'} placeholder="Choose where shortcuts are saved" />
              <button className={state === 'hover' ? 'browse-button simulated-hover' : 'browse-button'}><Icon name="folder" size={17} /><span>Choose</span></button>
              <button className="browse-button" disabled={state === 'empty'}><Icon name="external" size={16} /><span>Launch</span></button>
            </div>
            <p className={`field-message ${state === 'error' ? 'error-text' : ''}`}>{state === 'error' ? 'Choose a folder you have permission to write to.' : 'New .bat files will be saved here.'}</p>
          </div>
        </section>
        <section className="settings-section divided">
          <span className="section-label">BEHAVIOR</span>
          <Toggle checked={overwrite} onChange={() => setOverwrite(!overwrite)} label="Force overwrite" note="Replace shortcuts with matching names without asking." />
          <Toggle checked={contexts} onChange={() => setContexts(!contexts)} label="Right-click context menus" note="Add “Create keycut” to File Explorer menus." />
        </section>
        <div className="settings-footer">
          <button className="secondary-button">Cancel</button>
          <button className="primary-button"><Icon name="check" size={16} />Save changes</button>
        </div>
      </main>
    </div>
  )
}

type ShortcutType = 'URL' | 'File' | 'Folder' | 'Command'
type ShortcutRow = { name: string; type: ShortcutType; description: string; destination: string }
type SortKey = 'name' | 'type' | 'description'

const managerRows: ShortcutRow[] = [
  { name: 'l-vicki', type: 'URL', description: 'logs for vicki', destination: 'https://logs.example.com/vicki' },
  { name: 'downloads', type: 'Folder', description: 'Downloads folder', destination: 'C:\\...\\Downloads' },
  { name: 'admin', type: 'URL', description: 'admin dashboard', destination: 'https://admin.example.com' },
  { name: 'analytics', type: 'URL', description: 'weekly analytics', destination: 'https://analytics.example.com/weekly' },
  { name: 'calendar', type: 'URL', description: 'team calendar', destination: 'https://calendar.example.com/team' },
  { name: 'client-files', type: 'Folder', description: 'active client files', destination: 'C:\\Work\\Clients\\Active' },
  { name: 'design-system', type: 'URL', description: 'component library', destination: 'https://figma.com/files/design-system' },
  { name: 'expenses', type: 'File', description: 'expense tracker', destination: 'C:\\Work\\Finance\\expenses.xlsx' },
  { name: 'figma-team', type: 'URL', description: 'team projects', destination: 'https://figma.com/files/team/1842' },
  { name: 'github', type: 'URL', description: 'keycuts repository', destination: 'https://github.com/example/keycuts' },
  { name: 'invoices', type: 'Folder', description: 'current invoices', destination: 'C:\\Work\\Finance\\Invoices' },
  { name: 'meeting-notes', type: 'File', description: 'shared meeting notes', destination: 'C:\\Work\\Notes\\meetings.txt' },
  { name: 'projects', type: 'Folder', description: 'active projects', destination: 'C:\\Work\\Projects' },
  { name: 'reports', type: 'Folder', description: 'monthly reports', destination: 'C:\\Work\\Reports\\2025' },
  { name: 'restart-explorer', type: 'Command', description: 'restart Windows Explorer', destination: 'taskkill /f /im explorer.exe && start explorer.exe' },
  { name: 'screenshots', type: 'Folder', description: 'saved screenshots', destination: 'C:\\Users\\Maya\\Pictures\\Screenshots' },
  { name: 'staging', type: 'URL', description: 'staging environment', destination: 'https://staging.example.com' },
  { name: 'timesheets', type: 'URL', description: 'submit time', destination: 'https://time.example.com' },
  { name: 'uploads', type: 'Folder', description: 'pending uploads', destination: 'C:\\Work\\Uploads' },
  { name: 'weekly-log', type: 'File', description: 'weekly work log', destination: 'C:\\Work\\Notes\\weekly-log.txt' },
]

const typeIcons: Record<ShortcutType, IconName> = { URL: 'link', File: 'file', Folder: 'folder', Command: 'play' }

function BatmanagerPanel({ state, onClose }: { state: PreviewState; onClose: () => void }) {
  const [sort, setSort] = useState<{ key: SortKey; direction: 'asc' | 'desc' }>({ key: 'name', direction: 'asc' })
  const [query, setQuery] = useState('')
  const [openMenu, setOpenMenu] = useState<string | null>(state === 'hover' ? 'l-vicki' : null)
  const sourceRows = state === 'empty' ? [] : managerRows
  const rows = sourceRows
    .filter((row) => `${row.name} ${row.type} ${row.description} ${row.destination}`.toLowerCase().includes(query.toLowerCase()))
    .sort((a, b) => {
      const result = a[sort.key].localeCompare(b[sort.key])
      return sort.direction === 'asc' ? result : -result
    })
  const updateSort = (key: SortKey) => setSort((current) => ({ key, direction: current.key === key && current.direction === 'asc' ? 'desc' : 'asc' }))
  const sortHeader = (label: string, key: SortKey) => (
    <button className={sort.key === key ? `sort-button active sort-${sort.direction}` : 'sort-button'} onClick={() => updateSort(key)}>
      {label}<Icon name="chevron" size={12} /><span className="sr-only">{sort.key === key ? `Sorted ${sort.direction}ending` : 'Sort column'}</span>
    </button>
  )

  return (
      <main className="manager-panel" aria-label="Shortcut manager">
        <div className="manager-toolbar">
          <div><span className="eyebrow">KEYCUT MANAGER</span><h1>Your keycuts</h1><p>Scan, search, and manage every shortcut in one place.</p></div>
          <button className="secondary-button new-keycut-button" onClick={onClose}><Icon name="spark" size={15} />New keycut</button>
        </div>
        <div className="manager-tools">
          <div className="search-box"><Icon name="search" size={16} /><input aria-label="Search shortcuts" placeholder="Search names, types, or destinations" value={query} onChange={(event) => setQuery(event.target.value)} /></div>
          <span className="result-count">{rows.length} {rows.length === 1 ? 'keycut' : 'keycuts'}</span>
        </div>
        {rows.length ? (
          <div className="table-wrap">
            <table>
              <thead><tr><th>{sortHeader('Shortcut', 'name')}</th><th>{sortHeader('Type', 'type')}</th><th>{sortHeader('Description', 'description')}</th><th>Destination</th><th aria-label="Actions" /></tr></thead>
              <tbody>
                {rows.map((row) => <tr key={row.name} className={openMenu === row.name ? 'row-active' : ''}>
                  <td><strong>{row.name}</strong>{state === 'error' && row.name === 'meeting-notes' && <small className="row-error">File missing</small>}</td>
                  <td><span className={`type-badge type-${row.type.toLowerCase()}`}><Icon name={typeIcons[row.type]} size={12} />{row.type}</span></td>
                  <td><span className="description-value">{row.description}</span></td>
                  <td><span className="destination-value" title={row.destination}>{row.destination}</span></td>
                  <td className="menu-cell">
                    <button className="row-menu" aria-label={`Actions for ${row.name}`} aria-expanded={openMenu === row.name} onClick={() => setOpenMenu(openMenu === row.name ? null : row.name)}><Icon name="more" size={17} /></button>
                    {openMenu === row.name && (
                      <div className="context-menu">
                        <button><Icon name="play" size={16} />Open keycut</button>
                        <button><Icon name="file" size={16} />Edit</button>
                        <button><Icon name="copy" size={16} />Copy destination</button>
                        <span />
                        <button className="danger-menu"><Icon name="delete" size={16} />Delete</button>
                      </div>
                    )}
                  </td>
                </tr>)}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="empty-state">
            <div className="empty-illustration"><Icon name="file" size={32} /><span><Icon name="spark" size={14} /></span></div>
            <h2>No shortcuts yet</h2><p>Create your first shortcut in keycuts and it’ll show up here.</p>
            <button className="primary-button">Create a shortcut<Icon name="arrow" size={16} /></button>
          </div>
        )}
        <div className="manager-status"><span>{rows.length} items</span></div>
      </main>
  )
}

function StyleGuide() {
  const icons: IconName[] = ['settings', 'folder', 'file', 'link', 'external', 'search', 'more', 'copy', 'play', 'delete']
  return (
    <div className="guide">
      <div className="guide-header"><span className="eyebrow">KEYCUTS DESIGN SYSTEM</span><h1>Friendly precision for tiny tasks.</h1><p>A compact Windows utility language built from native-feeling controls, crisp geometry, and calm blue accents.</p></div>
      <section className="guide-section">
        <div className="guide-section-title"><span>01</span><div><h2>Color</h2><p>Semantic tokens meet WCAG AA in both themes.</p></div></div>
        <div className="swatch-grid">
          {[['Accent','var(--accent)'],['Accent hover','var(--accent-hover)'],['Canvas','var(--canvas)'],['Surface','var(--surface)'],['Text','var(--text)'],['Danger','var(--danger)']].map(([name,color]) => <div className="swatch" key={name}><i style={{background: color}} /><strong>{name}</strong><span>{color.replace('var(--','').replace(')','')}</span></div>)}
        </div>
      </section>
      <section className="guide-section split-guide">
        <div>
          <div className="guide-section-title"><span>02</span><div><h2>Typography</h2><p>Segoe UI, tuned for compact desktop reading.</p></div></div>
          <div className="type-samples"><span className="type-display">Create a keycut</span><span className="type-title">Shortcut settings</span><span className="type-body">Regular body text stays clear at 14px.</span><span className="type-label">COMPACT LABEL / 11PX</span></div>
        </div>
        <div>
          <div className="guide-section-title"><span>03</span><div><h2>Spacing</h2><p>A 4px base keeps controls aligned.</p></div></div>
          <div className="spacing-scale">{[4,8,12,16,24,32].map((n) => <div key={n}><span>{n}</span><i style={{width: `${n * 2}px`}} /></div>)}</div>
        </div>
      </section>
      <section className="guide-section">
        <div className="guide-section-title"><span>04</span><div><h2>Controls & states</h2><p>Every interactive element has hover, pressed, disabled, and keyboard focus treatment.</p></div></div>
        <div className="component-grid">
          <div className="component-demo"><small>BUTTONS</small><div><button className="primary-button">Default</button><button className="primary-button simulated-hover">Hover</button><button className="secondary-button focus-demo">Focus</button><button className="primary-button" disabled>Disabled</button></div></div>
          <div className="component-demo"><small>FIELDS</small><input defaultValue="Default field" /><input className="focus-demo" defaultValue="Focused field" /><input className="input-error" defaultValue="Error field" /></div>
        </div>
      </section>
      <section className="guide-section">
        <div className="guide-section-title"><span>05</span><div><h2>Icons</h2><p>16–20px outline icons, exported as SVG for XAML conversion.</p></div></div>
        <div className="icon-grid">{icons.map((icon) => <a href={`/icons/${icon}.svg`} download key={icon}><Icon name={icon} size={20} /><span>{icon}</span></a>)}</div>
      </section>
    </div>
  )
}

const views: { id: View; label: string; meta: string }[] = [
  { id: 'main', label: 'Main window', meta: 'Expandable manager' },
  { id: 'settings', label: 'Settings', meta: '440 × 390' },
  { id: 'guide', label: 'Style guide', meta: 'Tokens & assets' },
]

export default function App() {
  const [view, setView] = useState<View>('main')
  const [state, setState] = useState<PreviewState>('default')
  const [dark, setDark] = useState(false)
  const current = useMemo(() => views.find((item) => item.id === view)!, [view])
  return (
    <div className={`app ${dark ? 'dark' : ''}`}>
      <aside className="sidebar">
        <div className="app-brand"><div className="brand-mark large"><Icon name="spark" size={19} /></div><div><strong>keycuts</strong><span>UI specification</span></div></div>
        <nav>
          <span className="nav-label">WINDOWS</span>
          {views.map((item) => <button key={item.id} className={view === item.id ? 'active' : ''} onClick={() => setView(item.id)}><span><Icon name={item.id === 'main' ? 'spark' : item.id === 'settings' ? 'settings' : 'copy'} size={17} />{item.label}</span><small>{item.meta}</small></button>)}
        </nav>
        <div className="sidebar-note"><span>WPF READY</span><p>Solid fills, simple shadows, vector icons, and native control states.</p></div>
        <div className="theme-control"><span><Icon name={dark ? 'moon' : 'sun'} size={17} />{dark ? 'Dark theme' : 'Light theme'}</span><button className={`toggle ${dark ? 'on' : ''}`} onClick={() => setDark(!dark)} role="switch" aria-checked={dark}><span /></button></div>
      </aside>
      <div className="workspace">
        <header className="workspace-header">
          <div><span>DESIGN PREVIEW</span><h2>{current.label}</h2></div>
          {view !== 'guide' && <div className="state-tabs" aria-label="Preview state">{(['default','hover','error','empty'] as PreviewState[]).map((item) => <button className={state === item ? 'active' : ''} onClick={() => setState(item)} key={item}>{item}</button>)}</div>}
        </header>
        <div className={`stage ${view === 'guide' ? 'guide-stage' : ''}`}>
          {view === 'main' && <MainWindow key={state} state={state} />}
          {view === 'settings' && <SettingsWindow key={state} state={state} />}
          {view === 'guide' && <StyleGuide />}
        </div>
        <footer className="workspace-footer"><span><i />Interactive prototype</span><span>Segoe UI · 4px grid · WCAG AA</span></footer>
      </div>
    </div>
  )
}
