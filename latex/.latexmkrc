$out_dir = 'build';
$aux_dir = 'build';

# Keep LaTeX helper files in the same artifacts directory for both CLI and editor builds.
$emulate_aux = 1;

$pdf_mode = 1;
$bibtex = 'bibtex %O %B';

@default_files = ('main.tex');
